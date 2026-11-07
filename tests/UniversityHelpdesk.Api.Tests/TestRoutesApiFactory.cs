using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace UniversityHelpdesk.Api.Tests;

/// <summary>
/// The real API host plus the controllers defined in this test assembly, which add routes that
/// exist only in tests.
/// </summary>
public class TestRoutesApiFactory : HelpdeskApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(TestRoutesApiFactory).Assembly));
    }
}
