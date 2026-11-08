using System.Text.Json;

namespace UniversityHelpdesk.Api.Notifications;

/// <summary>The JSON body published to RabbitMQ. <c>EventId</c> is also sent as the AMQP message ID.</summary>
public record NotificationMessage(
    Guid EventId,
    string EventType,
    string RecipientEmail,
    DateTime OccurredAt,
    JsonElement Payload);
