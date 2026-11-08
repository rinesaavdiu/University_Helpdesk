using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UniversityHelpdesk.NotificationWorker.Tests;

public class HealthFileWriterTests
{
    [Fact]
    public async Task HealthFile_IsRemovedWhileTheWorkerIsNotConsuming()
    {
        var path = Path.Combine(Path.GetTempPath(), $"worker-health-{Guid.NewGuid():N}.alive");
        await File.WriteAllTextAsync(path, "left over from an earlier run");
        var consumer = new NotificationConsumer(
            new SmtpEmailSender(Options.Create(new SmtpOptions())),
            Options.Create(new RabbitMqOptions()),
            Options.Create(new SmtpOptions()),
            Options.Create(new DeliveryOptions()),
            NullLogger<NotificationConsumer>.Instance);
        var writer = new HealthFileWriter(
            consumer,
            Options.Create(new HealthOptions { FilePath = path, Interval = TimeSpan.FromMilliseconds(50) }),
            NullLogger<HealthFileWriter>.Instance);

        try
        {
            await writer.StartAsync(CancellationToken.None);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (File.Exists(path) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.False(consumer.IsConsuming);
            Assert.False(File.Exists(path));
        }
        finally
        {
            await writer.StopAsync(CancellationToken.None);
            File.Delete(path);
        }
    }
}
