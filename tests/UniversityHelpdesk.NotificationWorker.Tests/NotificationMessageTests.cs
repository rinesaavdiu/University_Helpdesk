using System.Text;
using MimeKit;

namespace UniversityHelpdesk.NotificationWorker.Tests;

public class NotificationMessageTests
{
    private const string ValidBody = """
        {"eventId":"01a1bb39-b056-753b-8996-6118363232c8","eventType":"TicketResolved",
         "recipientEmail":"student1@uni.test","occurredAt":"2026-11-08T12:54:42.337735Z",
         "payload":{"ticketId":1,"studentName":"Demo Student One","room":"A-204","category":"Projector",
                    "description":"The projector shows no image.","resolvedAt":"2026-11-08T12:54:42.337735Z"}}
        """;

    private static TicketResolvedMessage Parse(string body)
    {
        Assert.True(TicketResolvedMessage.TryParse(Encoding.UTF8.GetBytes(body), out var message, out var error), error);
        return message;
    }

    [Fact]
    public void TryParse_AcceptsTheBodyThatTheApiPublishes()
    {
        var message = Parse(ValidBody);

        Assert.Equal(Guid.Parse("01a1bb39-b056-753b-8996-6118363232c8"), message.EventId);
        Assert.Equal("student1@uni.test", message.RecipientEmail);
        Assert.Equal(1, message.Payload.TicketId);
        Assert.Equal("Projector", message.Payload.Category);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"eventId":"01a1bb39-b056-753b-8996-6118363232c8","eventType":"TicketCreated","recipientEmail":"a@uni.test","payload":{}}""")]
    [InlineData("""{"eventType":"TicketResolved","recipientEmail":"a@uni.test","payload":{}}""")]
    [InlineData("""{"eventId":"01a1bb39-b056-753b-8996-6118363232c8","eventType":"TicketResolved","payload":{}}""")]
    [InlineData("""{"eventId":"01a1bb39-b056-753b-8996-6118363232c8","eventType":"TicketResolved","recipientEmail":"a@uni.test"}""")]
    public void TryParse_RejectsBodiesThatCanNeverBeDelivered(string body)
    {
        Assert.False(TicketResolvedMessage.TryParse(Encoding.UTF8.GetBytes(body), out var message, out var error));
        Assert.Null(message);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void ResolutionEmail_IsAddressedToTheStudentWithAStableMessageId()
    {
        var message = Parse(ValidBody);

        var email = ResolutionEmail.Create(message, new SmtpOptions { FromAddress = "helpdesk@uni.test", FromName = "University Helpdesk" });

        var to = Assert.IsType<MailboxAddress>(Assert.Single(email.To));
        Assert.Equal("student1@uni.test", to.Address);
        Assert.Equal("helpdesk@uni.test", Assert.IsType<MailboxAddress>(Assert.Single(email.From)).Address);
        Assert.Equal("Your helpdesk ticket #1 has been resolved", email.Subject);
        Assert.Equal("01a1bb39-b056-753b-8996-6118363232c8@helpdesk.uni.test", email.MessageId);
        Assert.Contains("Room: A-204", email.TextBody);
        Assert.Contains("Category: Projector", email.TextBody);
        Assert.Contains("Resolved at: 2026-11-08 12:54 UTC", email.TextBody);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(4, 16)]
    [InlineData(5, 30)]
    [InlineData(100, 30)]
    public void RetryDelay_DoublesAndIsCapped(int failedAttempts, int expectedSeconds)
    {
        var options = new DeliveryOptions { FirstRetryDelay = TimeSpan.FromSeconds(2), MaxRetryDelay = TimeSpan.FromSeconds(30) };

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), options.RetryDelay(failedAttempts));
    }
}
