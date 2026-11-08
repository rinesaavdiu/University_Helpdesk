using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace UniversityHelpdesk.Api.Tests;

/// <summary>Health and metrics endpoints. The test host has no database, so readiness reports it unavailable.</summary>
public class MonitoringTests(TestRoutesApiFactory factory) : IClassFixture<TestRoutesApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> ScrapeAsync()
    {
        var response = await _client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static double Sample(string metrics, string name, string labels)
    {
        var match = Regex.Match(metrics, $@"^{Regex.Escape(name)}\{{{Regex.Escape(labels)}\}} (\S+)$", RegexOptions.Multiline);
        return match.Success ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    [Fact]
    public async Task Liveness_IsHealthyWithoutDependencies()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal(0, body.GetProperty("checks").GetArrayLength());
    }

    [Fact]
    public async Task Readiness_ReportsTheUnreachableDatabaseQuicklyWithoutDetails()
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Readiness took {stopwatch.Elapsed}.");
        var text = await response.Content.ReadAsStringAsync();
        var check = Assert.Single(JsonDocument.Parse(text).RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("database", check.GetProperty("name").GetString());
        Assert.Equal("Unhealthy", check.GetProperty("status").GetString());
        Assert.DoesNotContain("Exception", text);
        Assert.DoesNotContain("Host=", text);
    }

    [Fact]
    public async Task RequestMetrics_UseRouteTemplatesNotRawPaths()
    {
        var before = Sample(await ScrapeAsync(), "helpdesk_http_requests_total",
            "method=\"GET\",route=\"/tickets/{id:int}\",status_code=\"401\"");

        await _client.GetAsync("/tickets/41");
        await _client.GetAsync("/tickets/42");
        var metrics = await ScrapeAsync();

        Assert.Equal(before + 2, Sample(metrics, "helpdesk_http_requests_total",
            "method=\"GET\",route=\"/tickets/{id:int}\",status_code=\"401\""));
        Assert.DoesNotContain("route=\"/tickets/41\"", metrics);
        Assert.Contains("helpdesk_http_request_duration_seconds_bucket{method=\"GET\",route=\"/tickets/{id:int}\"", metrics);
    }

    [Fact]
    public async Task RequestMetrics_GroupUnknownPathsAndCountServerErrors()
    {
        await _client.GetAsync("/no-such-path/123");
        var before = Sample(await ScrapeAsync(), "helpdesk_http_request_errors_total",
            "method=\"GET\",route=\"/test-errors/throw\",status_code=\"500\"");

        var response = await _client.GetAsync("/test-errors/throw");
        var metrics = await ScrapeAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(before + 1, Sample(metrics, "helpdesk_http_request_errors_total",
            "method=\"GET\",route=\"/test-errors/throw\",status_code=\"500\""));
        Assert.Contains("route=\"unmatched\",status_code=\"404\"", metrics);
        Assert.DoesNotContain("no-such-path", metrics);
    }
}
