namespace UniversityHelpdesk.Api.Notifications;

/// <summary>Stored as JSON in <c>PendingNotifications.Payload</c> for <c>TicketResolved</c> events.</summary>
public record TicketResolvedPayload(
    int TicketId,
    string StudentName,
    string Room,
    string Category,
    string Description,
    DateTime ResolvedAt);
