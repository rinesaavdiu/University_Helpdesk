namespace UniversityHelpdesk.Api.Domain;

public class Ticket
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public User Student { get; set; } = null!;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public required string Room { get; set; }

    public required string Description { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
