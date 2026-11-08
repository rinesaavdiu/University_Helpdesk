using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace UniversityHelpdesk.Api.Tests;

/// <summary>
/// Runs the real API host in Development with explicit test settings. No database or user
/// secrets are needed; the JWT signing key is generated for each factory, and the connection
/// string points at a closed port so any accidental database access fails fast.
/// </summary>
public class HelpdeskApiFactory : WebApplicationFactory<Program>
{
    public const string TestIssuer = "UniversityHelpdesk.Tests";
    public const string TestAudience = "UniversityHelpdesk.Tests.Clients";

    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    protected virtual IDictionary<string, string?> ExtraSettings => new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration(configuration =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:SigningKey"] = SigningKey,
                ["ConnectionStrings:HelpdeskDatabase"] = "Host=127.0.0.1;Port=1;Database=unused;Timeout=1",
                ["OutboxPublisher:Enabled"] = "false",
            };
            foreach (var (key, value) in ExtraSettings)
            {
                settings[key] = value;
            }

            configuration.AddInMemoryCollection(settings);
        });
    }
}
