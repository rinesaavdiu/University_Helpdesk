using System.Net;
using System.Text.Json;

namespace UniversityHelpdesk.Api.Tests;

public class SwaggerSmokeTests : IClassFixture<HelpdeskApiFactory>
{
    private readonly HttpClient _client;

    public SwaggerSmokeTests(HelpdeskApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiDocument_ReturnsOkWithApiTitle()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var title = document.RootElement.GetProperty("info").GetProperty("title").GetString();
        Assert.Equal("University Helpdesk API", title);
    }

    [Fact]
    public async Task SwaggerUi_ReturnsOk()
    {
        var response = await _client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
