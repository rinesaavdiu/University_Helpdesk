using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace UniversityHelpdesk.NotificationWorker;

public interface IEmailSender
{
    /// <summary>Completes once the SMTP server has accepted the message; throws otherwise.</summary>
    Task SendAsync(MimeMessage email, CancellationToken cancellationToken);
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(MimeMessage email, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);

        using var client = new SmtpClient { Timeout = (int)settings.Timeout.TotalMilliseconds };
        await client.ConnectAsync(
            settings.Host,
            settings.Port,
            settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            timeout.Token);

        if (!string.IsNullOrEmpty(settings.UserName))
        {
            await client.AuthenticateAsync(settings.UserName, settings.Password ?? "", timeout.Token);
        }

        await client.SendAsync(email, timeout.Token);
        await client.DisconnectAsync(quit: true, CancellationToken.None);
    }
}
