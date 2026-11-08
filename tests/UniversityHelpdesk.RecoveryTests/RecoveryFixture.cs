using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using StackExchange.Redis;
using UniversityHelpdesk.Api.Categories;
using UniversityHelpdesk.Api.Domain;
using UniversityHelpdesk.Api.Tests;
using UniversityHelpdesk.Api.Tests.Integration;
using UniversityHelpdesk.NotificationWorker;

namespace UniversityHelpdesk.RecoveryTests;

[CollectionDefinition(Name)]
public class RecoveryCollection : ICollectionFixture<RecoveryFixture>
{
    public const string Name = "Recovery";
}

/// <summary>
/// Shared state for one run of the recovery checks. Everything the run creates is tagged with its
/// run ID and removed afterwards: accounts and tickets in the test database, an exchange and two
/// queues in RabbitMQ, emails in Mailpit, and the category cache key in a separate Redis database.
/// </summary>
public sealed class RecoveryFixture : IAsyncLifetime
{
    /// <summary>Keeps the cache entry away from the one a development API uses in database 0.</summary>
    public const int RedisDatabase = 15;

    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(60);

    private IConnection _rabbit = null!;
    private readonly HttpClient _mailpit = new();

    public TestDatabase Database { get; private set; } = null!;
    public TestAccount Student { get; private set; } = null!;
    public TestAccount Technician { get; private set; } = null!;
    public string Exchange { get; private set; } = "";
    public string Queue { get; private set; } = "";
    public string FailedQueue { get; private set; } = "";

    public async Task InitializeAsync()
    {
        if (RecoverySettings.Missing.Count > 0)
        {
            return;
        }

        Database = new TestDatabase(IntegrationSettings.RequireTestDatabase(), "recovery");
        await Database.PrepareAsync();
        Student = await Database.CreateAccountAsync("student", UserRole.Student);
        Technician = await Database.CreateAccountAsync("technician", UserRole.Technician);

        Exchange = $"helpdesk.events.test-{Database.RunId}";
        Queue = $"helpdesk.ticket-resolved.test-{Database.RunId}";
        FailedQueue = $"helpdesk.ticket-resolved.failed.test-{Database.RunId}";
        _rabbit = await new ConnectionFactory { Uri = RecoverySettings.RabbitMq, ClientProvidedName = "Recovery checks" }
            .CreateConnectionAsync();
        _mailpit.BaseAddress = RecoverySettings.Mailpit;
        await DeleteCacheKeyAsync();
    }

    /// <summary>The real API host with the outbox publisher on, polling and retrying quickly.</summary>
    public HelpdeskApiFactory CreateApi(int? rabbitPort = null, string redis = "")
    {
        var settings = RabbitSettings(rabbitPort);
        settings["ConnectionStrings:HelpdeskDatabase"] = Database.ConnectionString;
        settings["OutboxPublisher:Enabled"] = "true";
        settings["OutboxPublisher:PollInterval"] = "00:00:00.200";
        settings["OutboxPublisher:FirstRetryDelay"] = "00:00:00.500";
        settings["OutboxPublisher:MaxRetryDelay"] = "00:00:01";
        settings["OutboxPublisher:PublishTimeout"] = "00:00:05";
        settings["Redis:Configuration"] = redis;
        return new RecoveryApiFactory(settings);
    }

    public string RedisThrough(FaultProxy proxy) => $"127.0.0.1:{proxy.Port},defaultDatabase={RedisDatabase}";

    /// <summary>Starts the real Notification Worker in this process.</summary>
    public async Task<IHost> StartWorkerAsync(
        int? rabbitPort = null, int? smtpPort = null, int maxAttempts = 5, string firstRetryDelay = "00:00:00.500")
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = "Test",
        });
        builder.Logging.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning);
        var settings = RabbitSettings(rabbitPort);
        settings["RabbitMq:FailedQueue"] = FailedQueue;
        settings["RabbitMq:ReconnectDelay"] = "00:00:01";
        settings["Smtp:Host"] = RecoverySettings.Smtp.Host;
        settings["Smtp:Port"] = (smtpPort ?? RecoverySettings.Smtp.Port).ToString();
        settings["Smtp:Timeout"] = "00:00:05";
        settings["Delivery:MaxAttempts"] = maxAttempts.ToString();
        settings["Delivery:FirstRetryDelay"] = firstRetryDelay;
        settings["Delivery:MaxRetryDelay"] = "00:00:01";
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddNotificationWorker();
        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private Dictionary<string, string?> RabbitSettings(int? port)
    {
        var uri = RecoverySettings.RabbitMq;
        var credentials = uri.UserInfo.Split(':', 2);
        return new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = uri.Host,
            ["RabbitMq:Port"] = (port ?? (uri.IsDefaultPort ? 5672 : uri.Port)).ToString(),
            ["RabbitMq:VirtualHost"] = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')) is { Length: > 0 } vhost ? vhost : "/",
            ["RabbitMq:UserName"] = Uri.UnescapeDataString(credentials[0]),
            ["RabbitMq:Password"] = Uri.UnescapeDataString(credentials[1]),
            ["RabbitMq:Exchange"] = Exchange,
            ["RabbitMq:TicketResolvedQueue"] = Queue,
        };
    }

    /// <summary>Creates a ticket as the run's student and resolves it as the run's technician.</summary>
    public async Task<int> ResolveNewTicketAsync(HelpdeskApiFactory api)
    {
        var student = await LoginAsync(api, Student);
        var technician = await LoginAsync(api, Technician);
        var ticket = await student.CreateTicketAsync(room: "R-1", description: "Recovery check: the projector is broken.");
        await technician.ChangeStatusAsync(ticket.Id(), "InProgress", 1);
        await technician.ChangeStatusAsync(ticket.Id(), "Resolved", 2);
        return ticket.Id();
    }

    public static async Task<HttpClient> LoginAsync(HelpdeskApiFactory api, TestAccount account)
    {
        var client = api.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new { email = account.Email, password = account.Password });
        response.EnsureSuccessStatusCode();
        var token = (await response.ReadJsonAsync()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    public async Task<PendingNotification> NotificationForAsync(int ticketId)
    {
        await using var db = Database.CreateContext();
        return await db.PendingNotifications.AsNoTracking().SingleAsync(n => n.TicketId == ticketId);
    }

    public async Task<PendingNotification> WaitUntilPublishedAsync(int ticketId)
    {
        PendingNotification? notification = null;
        await WaitUntilAsync(async () => (notification = await NotificationForAsync(ticketId)).PublishedAt is not null,
            $"the outbox publishes the event for ticket {ticketId}");
        return notification!;
    }

    /// <summary>Message IDs in a queue. Unacknowledged reads go back to the queue when the channel closes.</summary>
    public async Task<List<BasicGetResult>> PeekAsync(string queue)
    {
        await using var channel = await _rabbit.CreateChannelAsync();
        var messages = new List<BasicGetResult>();
        while (await channel.BasicGetAsync(queue, autoAck: false) is { } message)
        {
            messages.Add(message);
        }

        return messages;
    }

    /// <summary>Messages ready for delivery; safe to call while a consumer is running.</summary>
    public async Task<uint> ReadyCountAsync(string queue)
    {
        await using var channel = await _rabbit.CreateChannelAsync();
        return (await channel.QueueDeclarePassiveAsync(queue)).MessageCount;
    }

    /// <summary>The Mailpit summary of the email for an event, or null if none was received.</summary>
    public async Task<JsonElement?> FindEmailAsync(Guid eventId)
    {
        foreach (var message in await EmailsToStudentAsync())
        {
            if (message.GetProperty("MessageID").GetString() == $"{eventId}@helpdesk.uni.test")
            {
                return message;
            }
        }

        return null;
    }

    public async Task<JsonElement> WaitForEmailAsync(Guid eventId)
    {
        JsonElement? email = null;
        await WaitUntilAsync(async () => (email = await FindEmailAsync(eventId)) is not null, $"Mailpit receives the email for event {eventId}");
        return email!.Value;
    }

    private async Task<List<JsonElement>> EmailsToStudentAsync()
    {
        var query = Uri.EscapeDataString($"to:\"{Student.Email}\"");
        var result = await _mailpit.GetFromJsonAsync<JsonElement>($"api/v1/search?query={query}&limit=200");
        return result.GetProperty("messages").EnumerateArray().ToList();
    }

    public async Task<long> CategoryCacheTtlSecondsAsync()
    {
        await using var redis = await ConnectRedisAsync();
        var ttl = await redis.GetDatabase(RedisDatabase).KeyTimeToLiveAsync(CategoryCache.Key);
        return ttl is null ? -1 : (long)ttl.Value.TotalSeconds;
    }

    private async Task DeleteCacheKeyAsync()
    {
        await using var redis = await ConnectRedisAsync();
        await redis.GetDatabase(RedisDatabase).KeyDeleteAsync(CategoryCache.Key);
    }

    private static Task<ConnectionMultiplexer> ConnectRedisAsync()
    {
        var (host, port) = RecoverySettings.Redis;
        return ConnectionMultiplexer.ConnectAsync($"{host}:{port}");
    }

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, string description, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultWait);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting until {description}.");
            }

            await Task.Delay(250);
        }
    }

    public static Task WaitUntilAsync(Func<bool> condition, string description, TimeSpan? timeout = null) =>
        WaitUntilAsync(() => Task.FromResult(condition()), description, timeout);

    public async Task DisposeAsync()
    {
        if (Database is null)
        {
            return;
        }

        var emails = await EmailsToStudentAsync();
        if (emails.Count > 0)
        {
            var ids = emails.Select(message => message.GetProperty("ID").GetString()).ToArray();
            await _mailpit.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "api/v1/messages")
            {
                Content = JsonContent.Create(new { IDs = ids }),
            });
        }

        // A fresh channel each time: the broker closes the channel if the queue or exchange is missing.
        foreach (var delete in new Func<IChannel, Task>[]
        {
            channel => channel.QueueDeleteAsync(Queue),
            channel => channel.QueueDeleteAsync(FailedQueue),
            channel => channel.ExchangeDeleteAsync(Exchange),
        })
        {
            try
            {
                await using var channel = await _rabbit.CreateChannelAsync();
                await delete(channel);
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
            {
                // Never declared in this run.
            }
        }

        await _rabbit.DisposeAsync();
        await DeleteCacheKeyAsync();
        await Database.CleanUpAsync();
        _mailpit.Dispose();
    }

    private sealed class RecoveryApiFactory(IDictionary<string, string?> settings) : HelpdeskApiFactory
    {
        protected override IDictionary<string, string?> ExtraSettings => settings;
    }
}
