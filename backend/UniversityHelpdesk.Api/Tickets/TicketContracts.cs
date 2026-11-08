using System.ComponentModel.DataAnnotations;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Tickets;

public record CreateTicketRequest(
    [Required, TrimmedLength(1, 50)] string Room,
    [Required] int? CategoryId,
    [Required, TrimmedLength(10, 2000)] string Description);

/// <summary>
/// Partial edit: fields that are left out keep their current value. <c>version</c> must match
/// the ticket's current version.
/// </summary>
public record UpdateTicketRequest(
    [TrimmedLength(1, 50)] string? Room,
    int? CategoryId,
    [TrimmedLength(10, 2000)] string? Description,
    [Required, Range(1, int.MaxValue)] int? Version) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Room is null && CategoryId is null && Description is null)
        {
            yield return new ValidationResult(
                "Provide at least one of room, categoryId, or description.", ["Body"]);
        }
    }
}

public class ListTicketsQuery
{
    [AllowedValues(null, nameof(TicketStatus.Open), nameof(TicketStatus.InProgress), nameof(TicketStatus.Resolved),
        ErrorMessage = "The status must be Open, InProgress, or Resolved.")]
    public string? Status { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public record TicketPage(IReadOnlyList<TicketResponse> Items, int Page, int PageSize, int TotalCount);

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
