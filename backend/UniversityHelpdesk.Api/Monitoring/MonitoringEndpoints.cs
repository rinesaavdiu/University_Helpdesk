using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;
using UniversityHelpdesk.Api.Persistence;

namespace UniversityHelpdesk.Api.Monitoring;

public static class MonitoringEndpoints
{
    public const string HealthPath = "/health";
    public const string LivenessPath = "/health/live";
    public const string ReadinessPath = "/health/ready";
    public const string MetricsPath = "/metrics";

    private const string ReadyTag = "ready";

    public static IServiceCollection AddHelpdeskMonitoring(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(3));
        return services;
    }

    public static WebApplication MapHelpdeskMonitoring(this WebApplication app)
    {
        // Liveness runs no checks: it only shows that the process is serving requests.
        app.MapHealthChecks(LivenessPath, new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponse,
        }).AllowAnonymous();

        // Readiness checks the dependencies a request cannot work without. Redis and RabbitMQ are
        // left out on purpose: categories fall back to PostgreSQL and events wait in the outbox.
        app.MapHealthChecks(ReadinessPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteResponse,
        }).AllowAnonymous();

        app.MapMetrics(MetricsPath).AllowAnonymous();
        return app;
    }

    /// <summary>Status and duration per check only; exception details stay out of the response.</summary>
    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
            }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonSerializerOptions.Web));
    }
}

public sealed class DatabaseHealthCheck(HelpdeskDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
        }
    }
}
