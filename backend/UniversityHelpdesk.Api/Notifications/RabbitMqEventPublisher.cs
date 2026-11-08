using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Notifications;

public interface IEventPublisher
{
    /// <summary>Connects and declares the exchange, the durable queue, and its binding.</summary>
    Task EnsureTopologyAsync(CancellationToken cancellationToken);

    /// <summary>Returns only after the broker has confirmed the message; throws otherwise.</summary>
    Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Keeps one connection and one confirm-enabled channel. Any failure discards them, so the next
/// attempt reconnects from scratch.
/// </summary>
public sealed class RabbitMqEventPublisher(IOptions<RabbitMqOptions> options) : IEventPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task EnsureTopologyAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await GetChannelAsync(cancellationToken);
        }
        catch
        {
            await ResetAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PublishAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                MessageId = message.EventId.ToString(),
                Type = message.EventType,
                ContentType = "application/json",
                Persistent = true,
                Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAt).ToUnixTimeSeconds()),
            };
            var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions.Web);

            // With confirmation tracking on, this completes when the broker acks the message and
            // throws if it is nacked or, because of mandatory, returned as unroutable.
            await channel.BasicPublishAsync(
                options.Value.Exchange, message.EventType, mandatory: true, properties, body, cancellationToken);
        }
        catch (PublishException exception) when (exception.IsReturn)
        {
            // Resetting means the next attempt re-declares the queue and binding before publishing.
            await ResetAsync();
            throw new InvalidOperationException(
                $"The broker returned the message as unroutable: no queue is bound to " +
                $"'{options.Value.Exchange}' with routing key '{message.EventType}'", exception);
        }
        catch
        {
            await ResetAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await ResetAsync();
        var settings = options.Value;
        if (string.IsNullOrEmpty(settings.Password))
        {
            throw new InvalidOperationException(
                $"'{RabbitMqOptions.SectionName}:Password' is not configured. Set it in user secrets or " +
                $"the environment variable '{RabbitMqOptions.SectionName}__Password'.");
        }

        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            VirtualHost = settings.VirtualHost,
            UserName = settings.UserName,
            Password = settings.Password,
            ClientProvidedName = "UniversityHelpdesk.Api outbox publisher",
            AutomaticRecoveryEnabled = false,
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await _channel.ExchangeDeclareAsync(
            settings.Exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(
            settings.TicketResolvedQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(
            settings.TicketResolvedQueue, settings.Exchange, PendingNotification.TicketResolved, cancellationToken: cancellationToken);
        return _channel;
    }

    private async Task ResetAsync()
    {
        var channel = _channel;
        var connection = _connection;
        _channel = null;
        _connection = null;

        try
        {
            if (channel is not null)
            {
                await channel.DisposeAsync();
            }

            if (connection is not null)
            {
                await connection.DisposeAsync();
            }
        }
        catch
        {
            // The connection is being thrown away; a failure to close it cleanly changes nothing.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        _gate.Dispose();
    }
}
