using System.Globalization;
using MimeKit;

namespace UniversityHelpdesk.NotificationWorker;

public static class ResolutionEmail
{
    public static MimeMessage Create(TicketResolvedMessage message, SmtpOptions smtp)
    {
        var payload = message.Payload;
        var email = new MimeMessage
        {
            // Derived from the event ID, so a redelivered event produces the same Message-ID.
            MessageId = $"{message.EventId}@helpdesk.uni.test",
            Subject = $"Your helpdesk ticket #{payload.TicketId} has been resolved",
            Body = new TextPart("plain")
            {
                Text = $"""
                    Hello {payload.StudentName},

                    Your ticket #{payload.TicketId} has been resolved.

                    Room: {payload.Room}
                    Category: {payload.Category}
                    Resolved at: {payload.ResolvedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC

                    Your report:
                    {payload.Description}

                    If the problem comes back, please report a new ticket.

                    University Helpdesk
                    """,
            },
        };
        email.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        email.To.Add(new MailboxAddress(payload.StudentName, message.RecipientEmail));
        email.Headers.Add("X-Helpdesk-Event-Id", message.EventId.ToString());
        return email;
    }
}
