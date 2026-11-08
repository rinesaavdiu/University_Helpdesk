using Microsoft.Extensions.Options;

namespace UniversityHelpdesk.NotificationWorker;

/// <summary>
/// The worker has no HTTP endpoint, so it reports health through a file: a container health
/// check treats a recently written file as healthy.
/// </summary>
public sealed class HealthFileWriter(
    NotificationConsumer consumer,
    IOptions<HealthOptions> options,
    ILogger<HealthFileWriter> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.FilePath))
        {
            return;
        }

        var wasHealthy = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            var healthy = consumer.IsConsuming;
            try
            {
                if (healthy)
                {
                    await File.WriteAllTextAsync(settings.FilePath, DateTimeOffset.UtcNow.ToString("O"), stoppingToken);
                }
                else
                {
                    File.Delete(settings.FilePath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("Could not update the health file {Path}: {Reason}", settings.FilePath, exception.Message);
            }

            if (healthy != wasHealthy)
            {
                logger.LogInformation(healthy ? "Worker is healthy: consuming." : "Worker is unhealthy: not consuming.");
                wasHealthy = healthy;
            }

            try
            {
                await Task.Delay(settings.Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        File.Delete(settings.FilePath);
    }
}
