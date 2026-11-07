namespace UniversityHelpdesk.Api.Tickets;

public enum TicketOutcome
{
    Success,
    InvalidField,
    NotFound,
    Conflict,
}

/// <summary>
/// The result of a ticket operation, independent of HTTP. The controller maps each outcome to
/// the status code documented in the API contract.
/// </summary>
public record TicketResult<T>(TicketOutcome Outcome, T? Value = default, string? Field = null, string? Message = null)
{
    public static TicketResult<T> Success(T value) => new(TicketOutcome.Success, value);

    public static TicketResult<T> InvalidField(string field, string message) =>
        new(TicketOutcome.InvalidField, Field: field, Message: message);

    public static TicketResult<T> NotFound() => new(TicketOutcome.NotFound);

    public static TicketResult<T> Conflict(string message) => new(TicketOutcome.Conflict, Message: message);
}
