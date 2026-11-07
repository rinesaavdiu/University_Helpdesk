using Microsoft.IdentityModel.JsonWebTokens;

namespace UniversityHelpdesk.Api.Authentication;

public static class HelpdeskClaimTypes
{
    public const string UserId = JwtRegisteredClaimNames.Sub;

    public const string Email = JwtRegisteredClaimNames.Email;

    public const string Role = "role";
}
