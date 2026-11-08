using System.Net;

namespace UniversityHelpdesk.Api.Tests;

public class CategoryEndpointTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task ListCategories_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
