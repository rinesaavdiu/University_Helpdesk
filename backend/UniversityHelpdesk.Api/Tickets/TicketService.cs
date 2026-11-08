using Microsoft.EntityFrameworkCore;
using UniversityHelpdesk.Api.Domain;
using UniversityHelpdesk.Api.Persistence;

namespace UniversityHelpdesk.Api.Tickets;

public class TicketService(HelpdeskDbContext db, TimeProvider timeProvider)
{
    public async Task<TicketResult<TicketResponse>> CreateAsync(
        int studentId, CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var category = await FindActiveCategoryAsync(request.CategoryId!.Value, cancellationToken);
        if (category is null)
        {
            return InactiveCategory<TicketResponse>();
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

    public async Task<TicketPage> ListAsync(
        int userId, bool isTechnician, ListTicketsQuery query, CancellationToken cancellationToken)
    {
        var tickets = Visible(userId, isTechnician);
        if (query.Status is not null)
        {
            var status = Enum.Parse<TicketStatus>(query.Status);
            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        var totalCount = await tickets.CountAsync(cancellationToken);
        var items = await tickets
            .OrderByDescending(ticket => ticket.CreatedAt)
            .ThenByDescending(ticket => ticket.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new TicketPage(items.Select(TicketResponse.From).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<TicketResult<TicketResponse>> GetAsync(
        int id, int userId, bool isTechnician, CancellationToken cancellationToken)
    {
        var ticket = await Visible(userId, isTechnician)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return ticket is null
            ? TicketResult<TicketResponse>.NotFound()
            : TicketResult<TicketResponse>.Success(TicketResponse.From(ticket));
    }

    public async Task<TicketResult<TicketResponse>> UpdateAsync(
        int id, int studentId, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        if (request.CategoryId is { } categoryId && await FindActiveCategoryAsync(categoryId, cancellationToken) is null)
        {
            return InactiveCategory<TicketResponse>();
        }

        var room = request.Room?.Trim();
        var description = request.Description?.Trim();
        var now = Now();

        // One conditional UPDATE: it only applies if the caller owns the ticket, the ticket is
        // still open, and nobody has changed it since the caller read this version.
        var updated = await db.Tickets
            .Where(ticket => ticket.Id == id
                && ticket.StudentId == studentId
                && ticket.Status == TicketStatus.Open
                && ticket.Version == request.Version)
            .ExecuteUpdateAsync(setters =>
            {
                if (room is not null)
                {
                    setters.SetProperty(ticket => ticket.Room, room);
                }

                if (request.CategoryId is { } newCategoryId)
                {
                    setters.SetProperty(ticket => ticket.CategoryId, newCategoryId);
                }

                if (description is not null)
                {
                    setters.SetProperty(ticket => ticket.Description, description);
                }

                setters.SetProperty(ticket => ticket.Version, ticket => ticket.Version + 1);
                setters.SetProperty(ticket => ticket.UpdatedAt, now);
            }, cancellationToken);

        if (updated == 0)
        {
            return await ExplainRejectedWriteAsync<TicketResponse>(id, studentId, cancellationToken);
        }

        return await GetAsync(id, studentId, isTechnician: false, cancellationToken);
    }

    public async Task<TicketResult<bool>> DeleteAsync(int id, int studentId, CancellationToken cancellationToken)
    {
        // The open-state check is part of the DELETE statement itself, so a concurrent status
        // change cannot slip in between a check and the delete.
        var deleted = await db.Tickets
            .Where(ticket => ticket.Id == id
                && ticket.StudentId == studentId
                && ticket.Status == TicketStatus.Open)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0
            ? await ExplainRejectedWriteAsync<bool>(id, studentId, cancellationToken)
            : TicketResult<bool>.Success(true);
    }

    public async Task<TicketResult<TicketResponse>> ChangeStatusAsync(
        int id, ChangeTicketStatusRequest request, CancellationToken cancellationToken)
    {
        var target = Enum.Parse<TicketStatus>(request.Status!);
        var now = Now();

        // One conditional UPDATE: it only applies if the ticket is still in the status that
        // directly precedes the target and nobody has changed it since the caller read this version.
        var updated = 0;
        if (TicketWorkflow.PreviousStatus(target) is { } expected)
        {
            updated = await db.Tickets
                .Where(ticket => ticket.Id == id
                    && ticket.Status == expected
                    && ticket.Version == request.Version)
                .ExecuteUpdateAsync(setters =>
                {
                    setters.SetProperty(ticket => ticket.Status, target);
                    setters.SetProperty(ticket => ticket.Version, ticket => ticket.Version + 1);
                    setters.SetProperty(ticket => ticket.UpdatedAt, now);

                    if (target == TicketStatus.Resolved)
                    {
                        setters.SetProperty(ticket => ticket.ResolvedAt, now);
                    }
                }, cancellationToken);
        }

        if (updated == 0)
        {
            return await ExplainRejectedStatusChangeAsync(id, target, cancellationToken);
        }

        return await GetAsync(id, userId: 0, isTechnician: true, cancellationToken);
    }

    private IQueryable<Ticket> Visible(int userId, bool isTechnician)
    {
        var tickets = db.Tickets.AsNoTracking().Include(ticket => ticket.Category);
        return isTechnician ? tickets : tickets.Where(ticket => ticket.StudentId == userId);
    }

    /// <summary>Works out why a conditional write affected no rows.</summary>
    private async Task<TicketResult<T>> ExplainRejectedWriteAsync<T>(
        int id, int studentId, CancellationToken cancellationToken)
    {
        var current = await db.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Id == id && ticket.StudentId == studentId)
            .Select(ticket => new { ticket.Status })
            .SingleOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            return TicketResult<T>.NotFound();
        }

        return current.Status != TicketStatus.Open
            ? TicketResult<T>.Conflict($"Only open tickets can be changed. This ticket is {current.Status}.")
            : TicketResult<T>.Conflict("The ticket was changed by another request. Reload it and try again.");
    }

    /// <summary>Works out why a conditional status change affected no rows.</summary>
    private async Task<TicketResult<TicketResponse>> ExplainRejectedStatusChangeAsync(
        int id, TicketStatus target, CancellationToken cancellationToken)
    {
        var current = await db.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Id == id)
            .Select(ticket => new { ticket.Status })
            .SingleOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            return TicketResult<TicketResponse>.NotFound();
        }

        return TicketWorkflow.NextStatus(current.Status) switch
        {
            null => TicketResult<TicketResponse>.Conflict(
                $"This ticket is {current.Status} and its status can no longer change."),
            var next when next != target => TicketResult<TicketResponse>.Conflict(
                $"This ticket is {current.Status}, so it can only move to {next}."),
            _ => TicketResult<TicketResponse>.Conflict(
                "The ticket was changed by another request. Reload it and try again."),
        };
    }

    private Task<Category?> FindActiveCategoryAsync(int categoryId, CancellationToken cancellationToken) =>
        db.Categories.SingleOrDefaultAsync(
            category => category.Id == categoryId && category.IsActive, cancellationToken);

    private static TicketResult<T> InactiveCategory<T>() =>
        TicketResult<T>.InvalidField("CategoryId", "The categoryId must reference an active category.");

    // PostgreSQL stores timestamps to the microsecond, so responses match what is read back later.
    private DateTime Now()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % 10));
    }
}
