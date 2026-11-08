using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace UniversityHelpdesk.NotificationWorker;

/// <summary>The JSON body the API publishes for a <c>TicketResolved</c> event.</summary>
public record TicketResolvedMessage(
    Guid EventId,
    string EventType,
    string RecipientEmail,
    DateTime OccurredAt,
    TicketResolvedPayload Payload)
{
    public const string TicketResolved = "TicketResolved";

    /// <summary>Returns false with a short reason for a body that can never be delivered.</summary>
    public static bool TryParse(
        ReadOnlySpan<byte> body, [NotNullWhen(true)] out TicketResolvedMessage? message, out string? error)
    {
        message = null;
        try
        {
            message = JsonSerializer.Deserialize<TicketResolvedMessage>(body, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            error = "The body is not valid JSON for a TicketResolved event.";
            return false;
        }

        error = message switch
        {
            null => "The body is empty.",
            { EventType: not TicketResolved } => $"Unsupported event type '{message.EventType}'.",
            { EventId: var id } when id == Guid.Empty => "The event ID is missing.",
            { RecipientEmail: null or "" } => "The recipient email is missing.",
            { Payload: null } => "The payload is missing.",
            _ => null,
        };
        if (error is not null || message is null)
        {
            message = null;
            return false;
        }

        return true;
    }
}

public record TicketResolvedPayload(
    int TicketId,
    string StudentName,
    string Room,
    string Category,
    string Description,
    DateTime ResolvedAt);
