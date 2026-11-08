using System.Text.Json;
using Microsoft.Extensions.Options;
using Prometheus;
using StackExchange.Redis;
using UniversityHelpdesk.Api.Tickets;

namespace UniversityHelpdesk.Api.Categories;

public class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis configuration string. Empty disables the cache.</summary>
    public string Configuration { get; set; } = "";

    public TimeSpan CategoryCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Upper bound for connecting and for each cache command before falling back to PostgreSQL.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Caches the active-category list in Redis. Every Redis failure is logged and reported as a miss,
/// so callers fall back to PostgreSQL and the endpoint keeps working without the cache.
/// </summary>
public sealed class CategoryCache(IOptions<RedisOptions> options, ILogger<CategoryCache> logger) : IAsyncDisposable
{
    public const string Key = "helpdesk:categories:active:v1";

    private static readonly Counter Lookups = Metrics.CreateCounter(
        "helpdesk_category_cache_lookups_total",
        "Active-category cache lookups by result: hit, miss, or error (Redis unavailable).",
        new CounterConfiguration { LabelNames = ["result"] });

    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private ConnectionMultiplexer? _connection;

    public async Task<IReadOnlyList<CategorySummary>?> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var database = await GetDatabaseAsync(cancellationToken);
            if (database is null)
            {
                return null;
            }

            var value = await database.StringGetAsync(Key).WaitAsync(options.Value.Timeout, cancellationToken);
            if (value.IsNullOrEmpty)
            {
                Lookups.WithLabels("miss").Inc();
                return null;
            }

            Lookups.WithLabels("hit").Inc();
            return JsonSerializer.Deserialize<List<CategorySummary>>((string)value!, JsonSerializerOptions.Web);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Lookups.WithLabels("error").Inc();
            logger.LogWarning("Redis is unavailable; serving categories from PostgreSQL. {Reason}", exception.Message);
            return null;
        }
    }

    public async Task SetAsync(IReadOnlyList<CategorySummary> categories, CancellationToken cancellationToken)
    {
        if (_connection is { IsConnected: false })
        {
            // The lookup just reported Redis as unavailable; skip the write instead of waiting again.
            return;
        }

        try
        {
            var database = await GetDatabaseAsync(cancellationToken);
            if (database is null)
            {
                return;
            }

            var json = JsonSerializer.Serialize(categories, JsonSerializerOptions.Web);
            await database.StringSetAsync(Key, json, options.Value.CategoryCacheDuration)
                .WaitAsync(options.Value.Timeout, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Could not store categories in Redis. {Reason}", exception.Message);
        }
    }

    private async Task<IDatabase?> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Configuration))
        {
            return null;
        }

        if (_connection is null)
        {
            await _connectGate.WaitAsync(cancellationToken);
            try
            {
                if (_connection is null)
                {
                    var configuration = ConfigurationOptions.Parse(settings.Configuration);
                    var timeoutMs = (int)settings.Timeout.TotalMilliseconds;
                    configuration.ConnectTimeout = timeoutMs;
                    configuration.SyncTimeout = timeoutMs;
                    configuration.AsyncTimeout = timeoutMs;
                    configuration.ConnectRetry = 1;
                    // Keep reconnecting in the background instead of failing every request with a new connect.
                    configuration.AbortOnConnectFail = false;
                    configuration.ClientName = "UniversityHelpdesk.Api";

                    _connection = await ConnectionMultiplexer.ConnectAsync(configuration)
                        .WaitAsync(settings.Timeout * 2, cancellationToken);
                }
            }
            finally
            {
                _connectGate.Release();
            }
        }

        if (!_connection.IsConnected)
        {
            // The multiplexer reconnects in the background; fail now instead of waiting for a timeout.
            throw new InvalidOperationException("Redis is not connected.");
        }

        return _connection.GetDatabase();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectGate.Dispose();
    }
}
