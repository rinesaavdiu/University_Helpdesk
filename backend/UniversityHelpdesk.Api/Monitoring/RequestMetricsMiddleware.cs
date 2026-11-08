using System.Diagnostics;
using Prometheus;

namespace UniversityHelpdesk.Api.Monitoring;

/// <summary>
/// Counts requests, server errors, and latency per route template (for example
/// <c>/tickets/{id:int}</c>), never per raw path, so IDs do not create new label values.
/// Also writes one structured log line per API request with the template, status, and duration.
/// </summary>
public sealed class RequestMetricsMiddleware(RequestDelegate next, ILogger<RequestMetricsMiddleware> logger)
{
    private static readonly string[] KnownMethods = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

    private static readonly Counter Requests = Metrics.CreateCounter(
        "helpdesk_http_requests_total",
        "HTTP requests by method, route template, and status code.",
        new CounterConfiguration { LabelNames = ["method", "route", "status_code"] });

    private static readonly Counter Errors = Metrics.CreateCounter(
        "helpdesk_http_request_errors_total",
        "HTTP requests that ended with a 5xx status code, including unhandled exceptions.",
        new CounterConfiguration { LabelNames = ["method", "route", "status_code"] });

    private static readonly Histogram Duration = Metrics.CreateHistogram(
        "helpdesk_http_request_duration_seconds",
        "HTTP request latency by method and route template.",
        new HistogramConfiguration
        {
            LabelNames = ["method", "route"],
            Buckets = Histogram.ExponentialBuckets(start: 0.005, factor: 2, count: 12),
        });

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments(MonitoringEndpoints.MetricsPath))
        {
            await next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        var statusCode = StatusCodes.Status500InternalServerError;
        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            // An exception still propagating here becomes a 500 in the exception handler outside.
            var elapsed = Stopwatch.GetElapsedTime(started);
            var method = KnownMethods.Contains(context.Request.Method) ? context.Request.Method : "OTHER";
            var route = RouteTemplate(context);
            var status = statusCode.ToString();

            Requests.WithLabels(method, route, status).Inc();
            Duration.WithLabels(method, route).Observe(elapsed.TotalSeconds);
            if (statusCode >= 500)
            {
                Errors.WithLabels(method, route, status).Inc();
            }

            if (!context.Request.Path.StartsWithSegments(MonitoringEndpoints.HealthPath))
            {
                logger.LogInformation(
                    "HTTP {Method} {Route} responded {StatusCode} in {ElapsedMs:0.0} ms",
                    method, route, statusCode, elapsed.TotalMilliseconds);
            }
        }
    }

    private static string RouteTemplate(HttpContext context) =>
        context.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { } template }
            ? "/" + template.TrimStart('/')
            : "unmatched";
}
