using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Tests;

/// <summary>
/// Authentication, role, and request-validation behavior of the ticket endpoints. These checks
/// happen before any database access, so they run without PostgreSQL.
/// </summary>
public class TicketEndpointTests : IClassFixture<HelpdeskApiFactory>
{
    private const string ValidCreateBody =
        "{\"room\":\"A-204\",\"categoryId\":3,\"description\":\"The projector shows no image.\"}";

    private readonly HelpdeskApiFactory _factory;
    private readonly HttpClient _client;

    public TicketEndpointTests(HelpdeskApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string path, string? body, UserRole? role)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        if (role is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(_factory, 1, role.Value));
        }

        return _client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadErrors(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("errors");
    }

    [Fact]
    public async Task CreateTicket_WithoutToken_Returns401()
    {
        var response = await Send(HttpMethod.Post, "/tickets", ValidCreateBody, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTicket_AsTechnician_Returns403()
    {
        var response = await Send(HttpMethod.Post, "/tickets", ValidCreateBody, UserRole.Technician);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTicket_WithMissingFields_Returns400ForEachField()
    {
        var errors = await ReadErrors(await Send(HttpMethod.Post, "/tickets", "{}", UserRole.Student));

        Assert.True(errors.TryGetProperty("room", out _));
        Assert.True(errors.TryGetProperty("categoryId", out _));
        Assert.True(errors.TryGetProperty("description", out _));
    }

    [Theory]
    [InlineData("{\"room\":\"   \",\"categoryId\":3,\"description\":\"The projector shows no image.\"}", "room")]
    [InlineData("{\"room\":\"A-204\",\"categoryId\":3,\"description\":\"   too short   \"}", "description")]
    [InlineData("{\"room\":\"A-204\",\"categoryId\":3,\"description\":\"The projector shows no image.\",\"status\":\"Resolved\"}", "status")]
    [InlineData("{\"room\":\"A-204\",\"categoryId\":3,\"description\":\"The projector shows no image.\",\"studentId\":2}", "studentId")]
    public async Task CreateTicket_WithInvalidOrServerControlledField_Returns400(string body, string field)
    {
        var errors = await ReadErrors(await Send(HttpMethod.Post, "/tickets", body, UserRole.Student));

        Assert.True(errors.TryGetProperty(field, out _), $"Expected an error for '{field}'.");
    }

    [Fact]
    public async Task CreateTicket_WithWrongValueType_Returns400NamingTheField()
    {
        var body = "{\"room\":\"A-204\",\"categoryId\":\"three\",\"description\":\"The projector shows no image.\"}";

        var errors = await ReadErrors(await Send(HttpMethod.Post, "/tickets", body, UserRole.Student));

        Assert.Equal("The value has the wrong type.", errors.GetProperty("categoryId")[0].GetString());
    }

    [Fact]
    public async Task CreateTicket_WithTooLongRoom_Returns400()
    {
        var body = $"{{\"room\":\"{new string('R', 51)}\",\"categoryId\":3,\"description\":\"The projector shows no image.\"}}";

        var errors = await ReadErrors(await Send(HttpMethod.Post, "/tickets", body, UserRole.Student));

        Assert.True(errors.TryGetProperty("room", out _));
    }
}
