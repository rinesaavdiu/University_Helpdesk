namespace UniversityHelpdesk.Api.Domain;

/// <summary>An outbox row: an event saved with the change that caused it, waiting to be published.</summary>
public class PendingNotification
{
    public const string TicketResolved = "TicketResolved";

    /// <summary>Stable event ID; the same value travels with every publish attempt.</summary>
    public Guid Id { get; set; }

    public int TicketId { get; set; }

    public Ticket Ticket { get; set; } = null!;

    public required string RecipientEmail { get; set; }

    public required string EventType { get; set; }

    /// <summary>JSON with the details needed to write the email.</summary>
    public required string Payload { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int Attempts { get; set; }
}
