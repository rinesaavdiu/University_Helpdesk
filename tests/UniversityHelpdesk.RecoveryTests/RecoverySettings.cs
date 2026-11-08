using UniversityHelpdesk.Api.Tests.Integration;

namespace UniversityHelpdesk.RecoveryTests;

/// <summary>Where the real services for the recovery checks run, read from environment variables.</summary>
public static class RecoverySettings
{
    public const string RabbitMqVariable = "HELPDESK_TEST_RABBITMQ";
    public const string SmtpVariable = "HELPDESK_TEST_SMTP";
    public const string MailpitVariable = "HELPDESK_TEST_MAILPIT";
    public const string RedisVariable = "HELPDESK_TEST_REDIS";

    private static readonly string[] Required =
        [IntegrationSettings.DatabaseVariable, RabbitMqVariable, SmtpVariable, MailpitVariable, RedisVariable];

    public static IReadOnlyList<string> Missing => Required.Where(name => IntegrationSettings.Read(name) is null).ToList();

    /// <summary>AMQP URI including the virtual host, for example amqp://user:password@127.0.0.1:5672/helpdesk.</summary>
    public static Uri RabbitMq => new(IntegrationSettings.Read(RabbitMqVariable)!);

    public static (string Host, int Port) Smtp => HostAndPort(SmtpVariable);

    public static (string Host, int Port) Redis => HostAndPort(RedisVariable);

    public static Uri Mailpit => new(IntegrationSettings.Read(MailpitVariable)!.TrimEnd('/') + "/");

    private static (string Host, int Port) HostAndPort(string variable)
    {
        var value = IntegrationSettings.Read(variable)!;
        var separator = value.LastIndexOf(':');
        return (value[..separator], int.Parse(value[(separator + 1)..]));
    }
}

/// <summary>A recovery check that runs only when every service in <see cref="RecoverySettings"/> is configured.</summary>
public sealed class RecoveryFactAttribute : FactAttribute
{
    public RecoveryFactAttribute()
    {
        var missing = RecoverySettings.Missing;
        if (missing.Count > 0)
        {
            Skip = $"Set {string.Join(", ", missing)} to run the recovery checks.";
        }
    }
}
