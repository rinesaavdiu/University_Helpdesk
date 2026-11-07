using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using UniversityHelpdesk.Api.Authentication;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Tests;

public class JwtAuthenticationTests : IClassFixture<TestRoutesApiFactory>
{
    private readonly TestRoutesApiFactory _factory;
    private readonly HttpClient _client;

    public JwtAuthenticationTests(TestRoutesApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static readonly User Student = new()
    {
        Id = 11,
        Email = "student1@uni.test",
        DisplayName = "Test Student",
        PasswordHash = "unused",
        Role = UserRole.Student,
    };

    private static readonly User Technician = new()
    {
        Id = 21,
        Email = "technician1@uni.test",
        DisplayName = "Test Technician",
        PasswordHash = "unused",
        Role = UserRole.Technician,
    };

    private string IssueToken(User user) =>
        _factory.Services.GetRequiredService<TokenService>().CreateToken(user).Value;

    private Task<HttpResponseMessage> GetWithToken(string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return _client.SendAsync(request);
    }

    private string CreateCustomToken(Action<SecurityTokenDescriptor> customize)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = HelpdeskApiFactory.TestIssuer,
            Audience = HelpdeskApiFactory.TestAudience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(60),
            Subject = new ClaimsIdentity(
            [
                new Claim(HelpdeskClaimTypes.UserId, "11"),
                new Claim(HelpdeskClaimTypes.Role, nameof(UserRole.Student)),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_factory.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };
        customize(descriptor);
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    [Fact]
    public async Task ValidToken_Authenticates_WithUserIdAndRole()
    {
        var response = await GetWithToken("/test-auth/me", IssueToken(Technician));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var identity = await response.Content.ReadFromJsonAsync<TestIdentity>();
        Assert.Equal(new TestIdentity("21", "Technician"), identity);
    }

    [Fact]
    public async Task MissingToken_Returns401()
    {
        var response = await GetWithToken("/test-auth/me", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var token = CreateCustomToken(descriptor =>
        {
            var past = DateTime.UtcNow.AddHours(-2);
            descriptor.IssuedAt = past;
            descriptor.NotBefore = past;
            descriptor.Expires = past.AddMinutes(60);
        });

        var response = await GetWithToken("/test-auth/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedToken_Returns401()
    {
        // Change the role in the payload of a real student token but keep its signature.
        var parts = IssueToken(Student).Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
        Assert.Contains("\"Student\"", payload);
        parts[1] = Base64UrlEncoder.Encode(payload.Replace("\"Student\"", "\"Technician\""));

        var response = await GetWithToken("/test-auth/technician-only", string.Join('.', parts));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_Returns401()
    {
        var token = CreateCustomToken(descriptor => descriptor.SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(new byte[48]), SecurityAlgorithms.HmacSha256));

        var response = await GetWithToken("/test-auth/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("WrongIssuer", HelpdeskApiFactory.TestAudience)]
    [InlineData(HelpdeskApiFactory.TestIssuer, "WrongAudience")]
    public async Task TokenWithWrongIssuerOrAudience_Returns401(string issuer, string audience)
    {
        var token = CreateCustomToken(descriptor =>
        {
            descriptor.Issuer = issuer;
            descriptor.Audience = audience;
        });

        var response = await GetWithToken("/test-auth/me", token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StudentToken_OnTechnicianOnlyRoute_Returns403()
    {
        var response = await GetWithToken("/test-auth/technician-only", IssueToken(Student));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TechnicianToken_OnTechnicianOnlyRoute_Returns200()
    {
        var response = await GetWithToken("/test-auth/technician-only", IssueToken(Technician));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void IssuedToken_ExpiresAfter60Minutes_AndCarriesDocumentedClaims()
    {
        var token = new JsonWebToken(IssueToken(Student));

        Assert.Equal(TimeSpan.FromMinutes(60), token.ValidTo - token.IssuedAt);
        Assert.Equal("11", token.Subject);
        Assert.Equal("student1@uni.test", token.GetClaim(HelpdeskClaimTypes.Email).Value);
        Assert.Equal("Student", token.GetClaim(HelpdeskClaimTypes.Role).Value);
        Assert.Equal(HelpdeskApiFactory.TestIssuer, token.Issuer);
        Assert.Equal(HelpdeskApiFactory.TestAudience, token.Audiences.Single());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"email\":\"not-an-email\",\"password\":\"x\"}")]
    [InlineData("{\"email\":\"student1@uni.test\"}")]
    public async Task Login_WithInvalidRequest_Returns400(string body)
    {
        var response = await _client.PostAsync(
            "/auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void MissingSigningKey_StopsStartupWithClearError()
    {
        using var factory = new MissingSigningKeyFactory();

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Jwt:SigningKey is not configured", exception.Message);
    }

    public record TestIdentity(string? UserId, string? Role);

    public class MissingSigningKeyFactory : HelpdeskApiFactory
    {
        protected override IDictionary<string, string?> ExtraSettings => new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "",
        };
    }
}

/// <summary>Routes that exist only in the test host.</summary>
[ApiController]
[Route("test-auth")]
public class TestAuthController : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public JwtAuthenticationTests.TestIdentity Me() =>
        new(User.FindFirstValue(HelpdeskClaimTypes.UserId), User.FindFirstValue(HelpdeskClaimTypes.Role));

    [HttpGet("technician-only")]
    [Authorize(Policy = Policies.Technician)]
    public IActionResult TechnicianOnly() => Ok();
}
