using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using UniversityHelpdesk.Api.Tests.Integration;
using UniversityHelpdesk.NotificationWorker;

namespace UniversityHelpdesk.RecoveryTests;

/// <summary>
/// End-to-end recovery checks with the real API, worker, PostgreSQL, RabbitMQ, Mailpit, and Redis.
/// Outages are simulated with <see cref="FaultProxy"/>, so the shared services keep running.
/// </summary>
[Collection(RecoveryCollection.Name)]
public class RecoveryChecks(RecoveryFixture fixture)
{
    [RecoveryFact]
    public async Task StoppedWorker_EventWaitsInTheQueue_AndIsEmailedOnceTheWorkerStarts()
    {
        await using var api = fixture.CreateApi();
        var ticketId = await fixture.ResolveNewTicketAsync(api);
        var notification = await fixture.WaitUntilPublishedAsync(ticketId);

        var queued = await fixture.PeekAsync(fixture.Queue);
        Assert.Contains(queued, message => message.BasicProperties.MessageId == notification.Id.ToString());
        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.Null(await fixture.FindEmailAsync(notification.Id));

        using var worker = await fixture.StartWorkerAsync();
        var email = await fixture.WaitForEmailAsync(notification.Id);
        Assert.Contains($"#{ticketId}", email.GetProperty("Subject").GetString());
        await RecoveryFixture.WaitUntilAsync(async () => await fixture.ReadyCountAsync(fixture.Queue) == 0, "the queue is drained");
        await worker.StopAsync();
    }

    [RecoveryFact]
    public async Task TemporarySmtpFailure_IsRetried_AndTheEmailIsSentWhenSmtpReturns()
    {
        var (smtpHost, smtpPort) = RecoverySettings.Smtp;
        await using var smtp = new FaultProxy(smtpHost, smtpPort);
        smtp.Cut();
        using var worker = await fixture.StartWorkerAsync(smtpPort: smtp.Port, maxAttempts: 30);
        await using var api = fixture.CreateApi();

        var ticketId = await fixture.ResolveNewTicketAsync(api);
        var notification = await fixture.WaitUntilPublishedAsync(ticketId);
        await RecoveryFixture.WaitUntilAsync(() => smtp.RejectedConnections >= 2, "the worker has retried SMTP at least twice");
        Assert.Null(await fixture.FindEmailAsync(notification.Id));

        smtp.Restore();
        await fixture.WaitForEmailAsync(notification.Id);
        Assert.DoesNotContain(await fixture.PeekAsync(fixture.FailedQueue),
            message => message.BasicProperties.MessageId == notification.Id.ToString());
        await worker.StopAsync();
    }

    [RecoveryFact]
    public async Task SmtpDownForEveryAttempt_MovesTheEventToTheFailedQueue()
    {
        var (smtpHost, smtpPort) = RecoverySettings.Smtp;
        await using var smtp = new FaultProxy(smtpHost, smtpPort);
        smtp.Cut();
        using var worker = await fixture.StartWorkerAsync(smtpPort: smtp.Port, maxAttempts: 2);
        await using var api = fixture.CreateApi();

        var ticketId = await fixture.ResolveNewTicketAsync(api);
        var notification = await fixture.WaitUntilPublishedAsync(ticketId);
        var eventId = notification.Id.ToString();

        RabbitMQ.Client.BasicGetResult? failed = null;
        await RecoveryFixture.WaitUntilAsync(async () =>
                (failed = (await fixture.PeekAsync(fixture.FailedQueue)).FirstOrDefault(m => m.BasicProperties.MessageId == eventId)) is not null,
            "the event is moved to the failed queue");
        var headers = failed!.BasicProperties.Headers!;
        Assert.Equal(2, Convert.ToInt32(headers["x-delivery-attempts"]));
        Assert.NotEmpty(Encoding.UTF8.GetString((byte[])headers["x-failure-reason"]!));
        Assert.True(smtp.RejectedConnections >= 2);
        Assert.Null(await fixture.FindEmailAsync(notification.Id));
        await worker.StopAsync();
    }

    [RecoveryFact]
    public async Task BrokerOutage_EventWaitsInTheOutbox_AndPublisherAndWorkerRecover()
    {
        var rabbit = RecoverySettings.RabbitMq;
        await using var broker = new FaultProxy(rabbit.Host, rabbit.IsDefaultPort ? 5672 : rabbit.Port);
        using var worker = await fixture.StartWorkerAsync(rabbitPort: broker.Port);
        var consumer = worker.Services.GetRequiredService<NotificationConsumer>();
        await RecoveryFixture.WaitUntilAsync(() => consumer.IsConsuming, "the worker is consuming");
        await using var api = fixture.CreateApi(rabbitPort: broker.Port);
        var student = await RecoveryFixture.LoginAsync(api, fixture.Student);

        broker.Cut();
        await RecoveryFixture.WaitUntilAsync(() => !consumer.IsConsuming, "the worker notices the outage");
        var ticketId = await fixture.ResolveNewTicketAsync(api);
        await RecoveryFixture.WaitUntilAsync(async () => await fixture.NotificationForAsync(ticketId) is { Attempts: >= 2, PublishedAt: null },
            "publishing has failed at least twice");
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync($"/tickets/{ticketId}")).StatusCode);

        broker.Restore();
        var notification = await fixture.WaitUntilPublishedAsync(ticketId);
        await RecoveryFixture.WaitUntilAsync(() => consumer.IsConsuming, "the worker reconnects");
        await fixture.WaitForEmailAsync(notification.Id);
        await worker.StopAsync();
    }

    [RecoveryFact]
    public async Task RedisOutage_CategoriesFallBackToPostgres_AndCachingResumes()
    {
        var (redisHost, redisPort) = RecoverySettings.Redis;
        await using var redis = new FaultProxy(redisHost, redisPort);
        await using var api = fixture.CreateApi(redis: fixture.RedisThrough(redis));
        var client = await RecoveryFixture.LoginAsync(api, fixture.Student);

        var expected = await GetCategoriesAsync(client);
        var hitsBefore = await CacheLookupsAsync(client, "hit");
        Assert.Equal(expected, await GetCategoriesAsync(client));
        Assert.True(await CacheLookupsAsync(client, "hit") > hitsBefore, "The second request should be a cache hit.");
        Assert.InRange(await fixture.CategoryCacheTtlSecondsAsync(), 1, 300);

        redis.Cut();
        var errorsBefore = await CacheLookupsAsync(client, "error");
        for (var i = 0; i < 3; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            Assert.Equal(expected, await GetCategoriesAsync(client));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Fallback took {stopwatch.Elapsed}.");
        }

        Assert.True(await CacheLookupsAsync(client, "error") >= errorsBefore + 3, "Each lookup during the outage should count as an error.");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);

        redis.Restore();
        await RecoveryFixture.WaitUntilAsync(async () =>
        {
            var hits = await CacheLookupsAsync(client, "hit");
            Assert.Equal(expected, await GetCategoriesAsync(client));
            return await CacheLookupsAsync(client, "hit") > hits;
        }, "categories are served from Redis again");
    }

    private static async Task<string> GetCategoriesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/categories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<double> CacheLookupsAsync(HttpClient client, string result)
    {
        var metrics = await client.GetStringAsync("/metrics");
        var match = Regex.Match(metrics, $"^helpdesk_category_cache_lookups_total\\{{result=\"{result}\"\\}} (\\S+)$", RegexOptions.Multiline);
        return match.Success ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }
}
