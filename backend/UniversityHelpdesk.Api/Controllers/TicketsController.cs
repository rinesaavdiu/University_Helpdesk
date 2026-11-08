using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UniversityHelpdesk.Api.Authentication;
using UniversityHelpdesk.Api.Domain;
using UniversityHelpdesk.Api.Tickets;

namespace UniversityHelpdesk.Api.Controllers;

[ApiController]
[Route("tickets")]
[Authorize]
public class TicketsController(TicketService tickets, IOptions<ApiBehaviorOptions> apiBehavior) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var result = await tickets.CreateAsync(User.GetUserId(), request, cancellationToken);
        return ToActionResult(result, ticket => Created($"/tickets/{ticket.Id}", ticket));
    }

    [HttpGet]
    [ProducesResponseType<TicketPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<TicketPage> List([FromQuery] ListTicketsQuery query, CancellationToken cancellationToken) =>
        await tickets.ListAsync(User.GetUserId(), IsTechnician, query, cancellationToken);

    [HttpGet("{id:int}")]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var result = await tickets.GetAsync(id, User.GetUserId(), IsTechnician, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpPatch("{id:int}")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var result = await tickets.UpdateAsync(id, User.GetUserId(), request, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Student)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await tickets.DeleteAsync(id, User.GetUserId(), cancellationToken);
        return ToActionResult(result, _ => NoContent());
    }

    private bool IsTechnician => User.IsInRole(nameof(UserRole.Technician));

    private IActionResult ToActionResult<T>(TicketResult<T> result, Func<T, IActionResult> onSuccess)
    {
        switch (result.Outcome)
        {
            case TicketOutcome.Success:
                return onSuccess(result.Value!);
            case TicketOutcome.InvalidField:
                ModelState.AddModelError(result.Field!, result.Message!);
                return apiBehavior.Value.InvalidModelStateResponseFactory(ControllerContext);
            case TicketOutcome.NotFound:
                return Problem(statusCode: StatusCodes.Status404NotFound, detail: "The ticket was not found.");
            default:
                return Problem(statusCode: StatusCodes.Status409Conflict, detail: result.Message);
        }
    }
}
