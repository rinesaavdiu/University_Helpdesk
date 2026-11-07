using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using UniversityHelpdesk.Api.Authentication;
using UniversityHelpdesk.Api.Domain;
using UniversityHelpdesk.Api.ErrorHandling;

namespace UniversityHelpdesk.Api.Tests;

public class ProblemDetailsTests : IClassFixture<TestRoutesApiFactory>
{
    private const string LeakMarker = "leak-marker-7f3a";

    private readonly TestRoutesApiFactory _factory;
    private readonly HttpClient _client;

    public ProblemDetailsTests(TestRoutesApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static async Task<(JsonElement Body, string Raw)> AssertProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonDocument.Parse(raw).RootElement;
        var status = (int)expected;
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.Equal($"https://httpstatuses.io/{status}", body.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        return (body, raw);
    }

    private Task<HttpResponseMessage> PostLogin(string json) =>
        _client.PostAsync("/auth/login", new StringContent(json, Encoding.UTF8, "application/json"));

    private static string[] ErrorsFor(JsonElement body, string field) =>
        body.GetProperty("errors").GetProperty(field).EnumerateArray().Select(item => item.GetString()!).ToArray();

    [Fact]
    public async Task InvalidFields_Return400_WithFieldErrorsKeyedByJsonName()
    {
        var response = await PostLogin("{\"email\":\"not-an-email\"}");

        var (body, _) = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal("Bad Request", body.GetProperty("title").GetString());
        Assert.Equal(ProblemDetailsSetup.ValidationDetail, body.GetProperty("detail").GetString());
        Assert.NotEmpty(ErrorsFor(body, "email"));
        Assert.NotEmpty(ErrorsFor(body, "password"));
    }

    [Fact]
    public async Task MalformedJson_Returns400_WithoutEchoingTheBody()
    {
        var response = await PostLogin($"{{\"email\":\"{LeakMarker}\" oops");

        var (body, raw) = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal(["The request body must be valid JSON."], ErrorsFor(body, "body"));
        Assert.DoesNotContain(LeakMarker, raw);
        Assert.DoesNotContain("UniversityHelpdesk", raw);
    }

    [Fact]
    public async Task UnknownLoginField_Returns400_NamingTheField()
    {
        var response = await PostLogin("{\"email\":\"student1@uni.test\",\"password\":\"x\",\"role\":\"Technician\"}");

        var (body, raw) = await AssertProblem(response, HttpStatusCode.BadRequest);
        Assert.Equal(["This field is not allowed."], ErrorsFor(body, "role"));
        Assert.Single(body.GetProperty("errors").EnumerateObject());
        Assert.DoesNotContain("UniversityHelpdesk", raw);
    }

    [Fact]
    public async Task MissingToken_Returns401Problem_AndKeepsBearerChallenge()
    {
        var response = await _client.GetAsync("/test-auth/me");

        await AssertProblem(response, HttpStatusCode.Unauthorized);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task WrongRole_Returns403Problem()
    {
        var student = new User
        {
            Id = 11,
            Email = "student1@uni.test",
            DisplayName = "Test Student",
            PasswordHash = "unused",
            Role = UserRole.Student,
        };
        var token = _factory.Services.GetRequiredService<TokenService>().CreateToken(student).Value;
        var request = new HttpRequestMessage(HttpMethod.Get, "/test-auth/technician-only");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        await AssertProblem(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnmatchedRoute_Returns404Problem()
    {
        var response = await _client.GetAsync("/no-such-route");

        await AssertProblem(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnexpectedException_Returns500Problem_WithoutExceptionDetails()
    {
        var response = await _client.GetAsync("/test-errors/throw");

        var (body, raw) = await AssertProblem(response, HttpStatusCode.InternalServerError);
        Assert.Equal(ProblemDetailsSetup.UnexpectedErrorDetail, body.GetProperty("detail").GetString());
        Assert.DoesNotContain(LeakMarker, raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.DoesNotContain("TestErrorsController", raw);
    }

    [Fact]
    public async Task NoContentResponse_StaysEmpty()
    {
        var response = await _client.DeleteAsync("/test-errors/no-content");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentType);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }
}

/// <summary>Routes that exist only in the test host.</summary>
[ApiController]
[Route("test-errors")]
public class TestErrorsController : ControllerBase
{
    [HttpGet("throw")]
    public IActionResult Throw() =>
        throw new InvalidOperationException($"Simulated failure with internal detail {"leak-marker-7f3a"}.");

    [HttpDelete("no-content")]
    public IActionResult NoContentResult() => NoContent();
}
