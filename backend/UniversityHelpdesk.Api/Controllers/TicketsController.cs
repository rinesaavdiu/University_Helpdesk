using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UniversityHelpdesk.Api.Authentication;
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
