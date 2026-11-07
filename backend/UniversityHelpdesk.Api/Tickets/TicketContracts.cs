using System.ComponentModel.DataAnnotations;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Tickets;

public record CreateTicketRequest(
    [Required, TrimmedLength(1, 50)] string Room,
    [Required] int? CategoryId,
    [Required, TrimmedLength(10, 2000)] string Description);

public record CategorySummary(int Id, string Name);

public record TicketResponse(
    int Id,
    int StudentId,
    string Room,
    CategorySummary Category,
    string Description,
    string Status,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt)
{
    public static TicketResponse From(Ticket ticket) => new(
        ticket.Id,
        ticket.StudentId,
        ticket.Room,
        new CategorySummary(ticket.Category.Id, ticket.Category.Name),
        ticket.Description,
        ticket.Status.ToString(),
        ticket.Version,
        ticket.CreatedAt,
        ticket.UpdatedAt,
        ticket.ResolvedAt);
}
