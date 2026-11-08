using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace UniversityHelpdesk.NotificationWorker;

/// <summary>
/// Consumes <c>TicketResolved</c> events and emails the student. A message is acknowledged only
/// after the SMTP server accepts the email, so delivery is at least once: a crash between SMTP
/// acceptance and the acknowledgement makes RabbitMQ redeliver the message and the email is sent again.
/// </summary>
public sealed class NotificationConsumer(
    IEmailSender emailSender,
    IOptions<RabbitMqOptions> rabbitOptions,
    IOptions<SmtpOptions> smtpOptions,
    IOptions<DeliveryOptions> deliveryOptions,
    ILogger<NotificationConsumer> logger) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _consumeChannel;
    private IChannel? _failedChannel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = rabbitOptions.Value;
        if (string.IsNullOrEmpty(settings.Password))
        {
            logger.LogCritical(
                "'{Section}:Password' is not configured. Set it in user secrets or the environment variable '{Section}__Password'.",
                RabbitMqOptions.SectionName, RabbitMqOptions.SectionName);
            return;
        }

        _connection = await ConnectAsync(settings, stoppingToken);
        if (_connection is null)
        {
            return;
        }

        _consumeChannel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        _failedChannel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            stoppingToken);
        await DeclareTopologyAsync(_consumeChannel, settings, stoppingToken);

        // One unacknowledged message at a time: retries hold back later messages instead of reordering them.
        await _consumeChannel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(_consumeChannel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(delivery, stoppingToken);
        await _consumeChannel.BasicConsumeAsync(settings.TicketResolvedQueue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Consuming {Queue}.", settings.TicketResolvedQueue);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<IConnection?> ConnectAsync(RabbitMqOptions settings, CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            VirtualHost = settings.VirtualHost,
            UserName = settings.UserName,
            Password = settings.Password,
            ClientProvidedName = "UniversityHelpdesk.NotificationWorker",
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                return await factory.CreateConnectionAsync(stoppingToken);
            }
            catch (BrokerUnreachableException exception)
            {
                logger.LogWarning(
                    "RabbitMQ is unreachable ({Reason}). Retrying in {RetryDelay}.",
                    exception.InnerException?.Message ?? exception.Message, settings.ReconnectDelay);
            }

            try
            {
                await Task.Delay(settings.ReconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return null;
    }

    /// <summary>Same declarations as the API, so either side can start first.</summary>
    private static async Task DeclareTopologyAsync(IChannel channel, RabbitMqOptions settings, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            settings.Exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            settings.TicketResolvedQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            settings.TicketResolvedQueue, settings.Exchange, TicketResolvedMessage.TicketResolved, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            settings.FailedQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
    }

    private async Task HandleAsync(BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        var channel = _consumeChannel!;
        var eventId = delivery.BasicProperties.MessageId ?? "unknown";
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["NotificationEventId"] = eventId });

        try
        {
            if (!TicketResolvedMessage.TryParse(delivery.Body.Span, out var message, out var error))
            {
                logger.LogError("Event {NotificationEventId} cannot be delivered: {Reason}", eventId, error);
                await MoveToFailedQueueAsync(delivery, error!, attempts: 0);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
                return;
            }

            if (delivery.Redelivered)
            {
                logger.LogInformation(
                    "Event {NotificationEventId} is a redelivery; the email may already have been sent once.", eventId);
            }

            var policy = deliveryOptions.Value;
            var email = ResolutionEmail.Create(message, smtpOptions.Value);
            string reason = "";
            for (var attempt = 1; attempt <= policy.MaxAttempts; attempt++)
            {
                try
                {
                    await emailSender.SendAsync(email, stoppingToken);
                    await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
                    logger.LogInformation(
                        "Sent the resolution email for ticket {TicketId}, event {NotificationEventId}, on attempt {Attempt}.",
                        message.Payload.TicketId, eventId, attempt);
                    return;
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested && IsSmtpFailure(exception))
                {
                    reason = Describe(exception);
                    if (attempt == policy.MaxAttempts)
                    {
                        break;
                    }

                    var delay = policy.RetryDelay(attempt);
                    logger.LogWarning(
                        "Sending event {NotificationEventId} failed on attempt {Attempt} of {MaxAttempts}: {Reason}. Retrying in {RetryDelay}.",
                        eventId, attempt, policy.MaxAttempts, reason, delay);
                    await Task.Delay(delay, stoppingToken);
                }
            }

            logger.LogError(
                "Giving up on event {NotificationEventId} after {Attempts} attempts: {Reason}. Moving it to {FailedQueue}.",
                eventId, policy.MaxAttempts, reason, rabbitOptions.Value.FailedQueue);
            await MoveToFailedQueueAsync(delivery, reason, policy.MaxAttempts);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down mid-delivery: hand the message back so it is delivered after a restart.
            await TryRequeueAsync(channel, delivery, eventId);
        }
        catch (Exception exception)
        {
            // Includes failing to move the message to the failed queue: keep it rather than lose it.
            logger.LogError(exception, "Handling event {NotificationEventId} failed; returning it to the queue.", eventId);
            try
            {
                await Task.Delay(deliveryOptions.Value.FirstRetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }

            await TryRequeueAsync(channel, delivery, eventId);
        }
    }

    /// <summary>
    /// Publishes a copy to the failed queue and waits for the broker's confirmation. Mandatory
    /// publishing makes a missing queue fail here instead of silently dropping the message.
    /// </summary>
    private async Task MoveToFailedQueueAsync(BasicDeliverEventArgs delivery, string reason, int attempts)
    {
        var original = delivery.BasicProperties;
        var properties = new BasicProperties
        {
            MessageId = original.MessageId,
            Type = original.Type,
            ContentType = original.ContentType,
            Timestamp = original.Timestamp,
            Persistent = true,
            Headers = new Dictionary<string, object?>
            {
                ["x-failure-reason"] = reason,
                ["x-delivery-attempts"] = attempts,
                ["x-failed-at"] = DateTimeOffset.UtcNow.ToString("O"),
            },
        };

        try
        {
            await _failedChannel!.BasicPublishAsync(
                exchange: "", rabbitOptions.Value.FailedQueue, mandatory: true, properties, delivery.Body, CancellationToken.None);
        }
        catch (PublishException exception) when (exception.IsReturn)
        {
            throw new InvalidOperationException(
                $"The failed-message queue '{rabbitOptions.Value.FailedQueue}' does not exist; the message was returned.", exception);
        }
    }

    private async Task TryRequeueAsync(IChannel channel, BasicDeliverEventArgs delivery, string eventId)
    {
        try
        {
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // The channel is gone; RabbitMQ requeues unacknowledged messages when it closes.
            logger.LogWarning("Could not return event {NotificationEventId} to the queue: {Reason}", eventId, exception.Message);
        }
    }

    private static bool IsSmtpFailure(Exception exception) =>
        exception is SmtpCommandException or SmtpProtocolException or ServiceNotConnectedException
            or SocketException or IOException or TimeoutException or OperationCanceledException;

    /// <summary>A short reason without addresses or message content.</summary>
    private static string Describe(Exception exception) => exception switch
    {
        SmtpCommandException smtp => $"SMTP server rejected the message ({(int)smtp.StatusCode} {smtp.ErrorCode})",
        SmtpProtocolException => "SMTP protocol error",
        SocketException socket => $"cannot reach the SMTP server ({socket.SocketErrorCode})",
        OperationCanceledException or TimeoutException => "the SMTP server timed out",
        _ => exception.GetType().Name,
    };

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        foreach (var disposable in new IAsyncDisposable?[] { _consumeChannel, _failedChannel, _connection })
        {
            if (disposable is not null)
            {
                try
                {
                    await disposable.DisposeAsync();
                }
                catch
                {
                    // Shutting down; a connection that does not close cleanly is dropped by the broker.
                }
            }
        }

        logger.LogInformation("Notification worker stopped.");
    }
}
