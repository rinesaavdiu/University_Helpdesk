namespace UniversityHelpdesk.Api.Notifications;

public class OutboxPublisherOptions
{
    public const string SectionName = "OutboxPublisher";

    public bool Enabled { get; set; } = true;

    /// <summary>How long to wait after a cycle that found nothing to do or hit a failure.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Most events published in one cycle before polling again.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>After this many failed attempts an event is left unpublished for inspection.</summary>
    public int MaxAttempts { get; set; } = 10;

    public TimeSpan FirstRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Upper bound for one broker round trip, including the publisher confirmation.</summary>
    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Delay before the next attempt: doubles after each failure, capped at <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan RetryDelay(int failedAttempts)
    {
        var exponent = Math.Clamp(failedAttempts - 1, 0, 30);
        var delay = FirstRetryDelay * Math.Pow(2, exponent);
        return delay < MaxRetryDelay ? delay : MaxRetryDelay;
    }
}
