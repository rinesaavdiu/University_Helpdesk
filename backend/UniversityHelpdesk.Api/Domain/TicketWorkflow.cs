namespace UniversityHelpdesk.Api.Domain;

/// <summary>The one-way status workflow: Open → InProgress → Resolved.</summary>
public static class TicketWorkflow
{
    public static TicketStatus? NextStatus(TicketStatus current) => current switch
    {
        TicketStatus.Open => TicketStatus.InProgress,
        TicketStatus.InProgress => TicketStatus.Resolved,
        _ => null,
    };

    /// <summary>Whether reaching <paramref name="target"/> sends the student a confirmation email.</summary>
    public static bool NotifiesStudent(TicketStatus target) => target == TicketStatus.Resolved;

    /// <summary>The only status a ticket can move to <paramref name="target"/> from, if any.</summary>
    public static TicketStatus? PreviousStatus(TicketStatus target) => target switch
    {
        TicketStatus.InProgress => TicketStatus.Open,
        TicketStatus.Resolved => TicketStatus.InProgress,
        _ => null,
    };
}
