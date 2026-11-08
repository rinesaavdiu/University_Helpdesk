using UniversityHelpdesk.Api.Notifications;

namespace UniversityHelpdesk.Api.Tests;

public class OutboxPublisherOptionsTests
{
    private static readonly OutboxPublisherOptions Options = new()
    {
        FirstRetryDelay = TimeSpan.FromSeconds(5),
        MaxRetryDelay = TimeSpan.FromMinutes(5),
    };

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(6, 160)]
    public void RetryDelay_DoublesAfterEachFailure(int failedAttempts, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), Options.RetryDelay(failedAttempts));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(1000)]
    public void RetryDelay_IsCappedAtTheMaximum(int failedAttempts)
    {
        Assert.Equal(TimeSpan.FromMinutes(5), Options.RetryDelay(failedAttempts));
    }
}
