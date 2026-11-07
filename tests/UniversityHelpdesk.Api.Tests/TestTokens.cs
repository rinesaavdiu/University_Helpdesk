using Microsoft.Extensions.DependencyInjection;
using UniversityHelpdesk.Api.Authentication;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Tests;

/// <summary>Issues real tokens from the test host's TokenService without touching a database.</summary>
public static class TestTokens
{
    public static string For(HelpdeskApiFactory factory, int userId, UserRole role) =>
        factory.Services.GetRequiredService<TokenService>().CreateToken(new User
        {
            Id = userId,
            Email = $"user{userId}@uni.test",
            DisplayName = "Test User",
            PasswordHash = "unused",
            Role = role,
        }).Value;
}
