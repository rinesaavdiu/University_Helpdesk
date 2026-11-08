using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniversityHelpdesk.Api.Domain;
using UniversityHelpdesk.Api.Persistence;

namespace UniversityHelpdesk.Api.Notifications;

/// <summary>
/// Publishes unpublished <see cref="PendingNotification"/> rows. Each event is claimed with
/// <c>FOR UPDATE SKIP LOCKED</c> inside its own transaction, so several API replicas can run this
/// at once without publishing the same row in parallel. The row is marked published, in that same
/// transaction, only after the broker confirms the message.
/// </summary>
public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    IEventPublisher publisher,
    IOptions<OutboxPublisherOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Outbox publisher is disabled.");
            return;
        }

        logger.LogInformation("Outbox publisher started.");

        try
        {
            await publisher.EnsureTopologyAsync(stoppingToken);
            logger.LogInformation("RabbitMQ exchange, queue, and binding are declared.");
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // Not fatal: every publish attempt declares the topology again before sending.
            logger.LogWarning("Could not declare the RabbitMQ topology at startup: {Reason}", exception.Message);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = 0;
                while (published < settings.BatchSize && await PublishNextAsync(stoppingToken))
                {
                    published++;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Database trouble: nothing was marked, so the same rows are retried next cycle.
                logger.LogError(exception, "Outbox publisher cycle failed.");
            }

            try
            {
                await Task.Delay(settings.PollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Outbox publisher stopped.");
    }

    /// <summary>Claims and publishes one due event. Returns false when there is nothing to publish or it failed.</summary>
    private async Task<bool> PublishNextAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var notification = (await db.PendingNotifications
            .FromSql($"""
                SELECT * FROM "PendingNotifications"
                WHERE "PublishedAt" IS NULL
                  AND "Attempts" < {settings.MaxAttempts}
                  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {now})
                ORDER BY "CreatedAt"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(stoppingToken))
            .SingleOrDefault();

        if (notification is null)
        {
            return false;
        }

        var succeeded = true;
        notification.Attempts++;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeout.CancelAfter(settings.PublishTimeout);
            await publisher.PublishAsync(ToMessage(notification), timeout.Token);

            notification.PublishedAt = timeProvider.GetUtcNow().UtcDateTime;
            notification.NextAttemptAt = null;
            logger.LogInformation(
                "Published event {NotificationEventId} ({EventType}) on attempt {Attempt}.",
                notification.Id, notification.EventType, notification.Attempts);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            succeeded = false;
            if (notification.Attempts >= settings.MaxAttempts)
            {
                notification.NextAttemptAt = null;
                logger.LogError(exception,
                    "Giving up on event {NotificationEventId} after {Attempt} attempts; it stays unpublished for inspection.",
                    notification.Id, notification.Attempts);
            }
            else
            {
                var delay = settings.RetryDelay(notification.Attempts);
                notification.NextAttemptAt = now + delay;
                logger.LogWarning(
                    "Publishing event {NotificationEventId} failed on attempt {Attempt} of {MaxAttempts}: {Reason}. Retrying in {RetryDelay}.",
                    notification.Id, notification.Attempts, settings.MaxAttempts, exception.Message, delay);
            }
        }

        // The confirmation is in hand (or the failure recorded); a shutdown from here on must not
        // discard that, so the save ignores the stopping token.
        await db.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
        return succeeded;
    }

    private static NotificationMessage ToMessage(PendingNotification notification)
    {
        using var payload = JsonDocument.Parse(notification.Payload);
        return new NotificationMessage(
            notification.Id,
            notification.EventType,
            notification.RecipientEmail,
            notification.CreatedAt,
            payload.RootElement.Clone());
    }
}
