using Microsoft.Extensions.DependencyInjection;
using UniversityHelpdesk.Api.Persistence;

namespace UniversityHelpdesk.Api.Tests;

public class DatabaseConfigurationTests : IClassFixture<DatabaseConfigurationTests.MissingConnectionStringFactory>
{
    private readonly MissingConnectionStringFactory _factory;

    public DatabaseConfigurationTests(MissingConnectionStringFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ResolvingDbContext_WithoutConnectionString_ThrowsClearError()
    {
        using var scope = _factory.Services.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<HelpdeskDbContext>());

        Assert.Contains("ConnectionStrings__HelpdeskDatabase", exception.Message);
    }

    public class MissingConnectionStringFactory : HelpdeskApiFactory
    {
        protected override IDictionary<string, string?> ExtraSettings => new Dictionary<string, string?>
        {
            ["ConnectionStrings:HelpdeskDatabase"] = "",
        };
    }
}
