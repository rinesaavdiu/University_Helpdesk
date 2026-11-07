namespace UniversityHelpdesk.Api.Domain;

public class User
{
    public int Id { get; set; }

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Ticket> Tickets { get; } = [];
}
