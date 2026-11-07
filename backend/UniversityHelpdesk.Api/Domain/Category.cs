namespace UniversityHelpdesk.Api.Domain;

public class Category
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public bool IsActive { get; set; }

    public List<Ticket> Tickets { get; } = [];
}
