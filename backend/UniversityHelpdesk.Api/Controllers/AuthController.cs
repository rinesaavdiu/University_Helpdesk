using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityHelpdesk.Api.Authentication;

namespace UniversityHelpdesk.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(LoginService loginService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await loginService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "Invalid email or password.");
        }

        var user = result.User;
        return Ok(new LoginResponse(
            result.Token.Value,
            "Bearer",
            (int)result.Token.Lifetime.TotalSeconds,
            new LoginUser(user.Id, user.Email, user.DisplayName, user.Role.ToString())));
    }
}

public record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(128)] string Password);

public record LoginResponse(string AccessToken, string TokenType, int ExpiresIn, LoginUser User);

public record LoginUser(int Id, string Email, string DisplayName, string Role);
