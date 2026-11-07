using System.Security.Claims;

namespace UniversityHelpdesk.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the user ID from a validated token's <c>sub</c> claim.</summary>
    public static int GetUserId(this ClaimsPrincipal principal) =>
        int.Parse(principal.FindFirstValue(HelpdeskClaimTypes.UserId)
            ?? throw new InvalidOperationException("The token has no user ID claim."));
}
