using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UniversityHelpdesk.Api.Categories;
using UniversityHelpdesk.Api.Tickets;

namespace UniversityHelpdesk.Api.Tests;

public class CategoryCacheTests
{
    private static CategoryCache Create(string configuration) =>
        new(Options.Create(new RedisOptions { Configuration = configuration, Timeout = TimeSpan.FromMilliseconds(500) }),
            NullLogger<CategoryCache>.Instance);

    [Fact]
    public async Task Disabled_IsAlwaysAMiss()
    {
        await using var cache = Create("");

        await cache.SetAsync([new CategorySummary(1, "Audio")], CancellationToken.None);

        Assert.Null(await cache.GetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnreachableRedis_FallsBackWithinTheTimeout()
    {
        // Port 1 on loopback refuses connections, so this needs no Redis server.
        await using var cache = Create("127.0.0.1:1");
        var stopwatch = Stopwatch.StartNew();

        var first = await cache.GetAsync(CancellationToken.None);
        await cache.SetAsync([new CategorySummary(1, "Audio")], CancellationToken.None);
        var second = await cache.GetAsync(CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Fallback took {stopwatch.Elapsed}.");
    }
}
