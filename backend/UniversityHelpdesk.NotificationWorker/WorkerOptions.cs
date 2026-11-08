namespace UniversityHelpdesk.NotificationWorker;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "";

    /// <summary>Kept in user secrets or an environment variable, never in appsettings.</summary>
    public string Password { get; set; } = "";

    /// <summary>Must match the API: a durable direct exchange with the event type as routing key.</summary>
    public string Exchange { get; set; } = "helpdesk.events";

    /// <summary>Must match the API, which declares the same durable queue before publishing.</summary>
    public string TicketResolvedQueue { get; set; } = "helpdesk.ticket-resolved";

    /// <summary>Durable queue that keeps messages whose delivery was given up, for inspection.</summary>
    public string FailedQueue { get; set; } = "helpdesk.ticket-resolved.failed";

    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
}

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 1025;

    /// <summary>Mailpit accepts plain SMTP; set to true for a server that requires STARTTLS.</summary>
    public bool UseStartTls { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "helpdesk@uni.test";

    public string FromName { get; set; } = "University Helpdesk";

    /// <summary>Upper bound for one SMTP conversation: connect, send, and disconnect.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}

public class DeliveryOptions
{
    public const string SectionName = "Delivery";

    /// <summary>SMTP attempts per message before it is moved to the failed queue.</summary>
    public int MaxAttempts { get; set; } = 5;

    public TimeSpan FirstRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Delay before the next attempt: doubles after each failure, capped at <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan RetryDelay(int failedAttempts)
    {
        var exponent = Math.Clamp(failedAttempts - 1, 0, 30);
        var delay = FirstRetryDelay * Math.Pow(2, exponent);
        return delay < MaxRetryDelay ? delay : MaxRetryDelay;
    }
}
