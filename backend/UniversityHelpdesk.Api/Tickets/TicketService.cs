using Microsoft.EntityFrameworkCore;
using UniversityHelpdesk.Api.Domain;
using UniversityHelpdesk.Api.Persistence;

namespace UniversityHelpdesk.Api.Tickets;

public class TicketService(HelpdeskDbContext db, TimeProvider timeProvider)
{
    public async Task<TicketResult<TicketResponse>> CreateAsync(
        int studentId, CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .SingleOrDefaultAsync(candidate => candidate.Id == request.CategoryId && candidate.IsActive, cancellationToken);
        if (category is null)
        {
            return TicketResult<TicketResponse>.InvalidField(
                nameof(CreateTicketRequest.CategoryId), "The categoryId must reference an active category.");
        }

        var now = Now();
        var ticket = new Ticket
        {
            StudentId = studentId,
            Category = category,
            Room = request.Room.Trim(),
            Description = request.Description.Trim(),
            Status = TicketStatus.Open,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        return TicketResult<TicketResponse>.Success(TicketResponse.From(ticket));
    }

    // PostgreSQL stores timestamps to the microsecond, so responses match what is read back later.
    private DateTime Now()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % 10));
    }
}
