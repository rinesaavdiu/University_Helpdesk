namespace UniversityHelpdesk.Api.Notifications;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "";

    /// <summary>Kept in user secrets or an environment variable, never in appsettings.</summary>
    public string Password { get; set; } = "";

    /// <summary>Durable direct exchange; the event type is the routing key.</summary>
    public string Exchange { get; set; } = "helpdesk.events";

    /// <summary>
    /// Durable queue for <c>TicketResolved</c> events. The API declares and binds it before publishing,
    /// so messages are kept even if the Notification Worker has never started.
    /// </summary>
    public string TicketResolvedQueue { get; set; } = "helpdesk.ticket-resolved";
}
