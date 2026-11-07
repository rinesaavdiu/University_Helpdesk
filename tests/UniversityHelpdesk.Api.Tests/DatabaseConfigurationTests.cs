using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
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

    public class MissingConnectionStringFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:HelpdeskDatabase"] = "",
                }));
        }
    }
}
