using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace UniversityHelpdesk.Api.Tests.Integration;

/// <summary>Status transitions, the outbox row they create, and concurrent updates.</summary>
[Collection(ApiIntegrationCollection.Name)]
public class WorkflowIntegrationTests(ApiIntegrationFixture fixture)
{
    private const int Contenders = 8;

    [IntegrationFact]
    public async Task Status_AdvancesOneStepAtATime_AndResolutionQueuesOneNotification()
    {
        var student = await fixture.ClientForAsync(fixture.Student1);
        var technician = await fixture.ClientForAsync(fixture.Technician);
        var ticket = await student.CreateTicketAsync();
        var statusPath = $"/tickets/{ticket.Id()}/status";

        Assert.Equal(HttpStatusCode.Conflict, (await technician.PatchAsJsonAsync(statusPath, new { status = "Resolved", version = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await technician.PatchAsJsonAsync(statusPath, new { status = "Open", version = 1 })).StatusCode);

        var inProgress = await technician.ChangeStatusAsync(ticket.Id(), "InProgress", 1);
        Assert.Equal("InProgress", inProgress.GetProperty("status").GetString());
        Assert.Equal(2, inProgress.Version());
        Assert.Equal(HttpStatusCode.Conflict, (await technician.PatchAsJsonAsync(statusPath, new { status = "Open", version = 2 })).StatusCode);

        // Once work has started, the student can no longer change or withdraw the ticket.
        Assert.Equal(HttpStatusCode.Conflict, (await student.PatchAsJsonAsync($"/tickets/{ticket.Id()}", new { room = "Z-1", version = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await student.DeleteAsync($"/tickets/{ticket.Id()}")).StatusCode);

        var resolved = await technician.ChangeStatusAsync(ticket.Id(), "Resolved", 2);
        Assert.Equal("Resolved", resolved.GetProperty("status").GetString());
        Assert.Equal(3, resolved.Version());
        Assert.Equal(JsonValueKind.String, resolved.GetProperty("resolvedAt").ValueKind);
        foreach (var status in new[] { "Open", "InProgress", "Resolved" })
        {
            Assert.Equal(HttpStatusCode.Conflict, (await technician.PatchAsJsonAsync(statusPath, new { status, version = 3 })).StatusCode);
        }

        await using var db = fixture.Database.CreateContext();
        var notification = Assert.Single(await db.PendingNotifications.Where(n => n.TicketId == ticket.Id()).ToListAsync());
        Assert.Equal(fixture.Student1.Email, notification.RecipientEmail);
        Assert.Equal("TicketResolved", notification.EventType);
    }

    [IntegrationFact]
    public async Task ConcurrentEdits_WithTheSameVersion_OnlyOneSucceeds()
    {
        var student = await fixture.ClientForAsync(fixture.Student1);
        var ticket = await student.CreateTicketAsync();

        var responses = await Task.WhenAll(Enumerable.Range(1, Contenders).Select(i =>
            student.PatchAsJsonAsync($"/tickets/{ticket.Id()}", new { room = $"R-{i}", version = 1 })));

        var winner = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(response => response != winner),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        var winningRoom = (await winner.ReadJsonAsync()).GetProperty("room").GetString();
        var current = await (await student.GetAsync($"/tickets/{ticket.Id()}")).ReadJsonAsync();
        Assert.Equal(2, current.Version());
        Assert.Equal(winningRoom, current.GetProperty("room").GetString());
    }

    [IntegrationFact]
    public async Task ConcurrentResolutions_ResolveOnce_AndQueueOneNotification()
    {
        var student = await fixture.ClientForAsync(fixture.Student1);
        var technician = await fixture.ClientForAsync(fixture.Technician);
        var ticket = await student.CreateTicketAsync();
        await technician.ChangeStatusAsync(ticket.Id(), "InProgress", 1);

        var responses = await Task.WhenAll(Enumerable.Range(1, Contenders).Select(_ =>
            technician.PatchAsJsonAsync($"/tickets/{ticket.Id()}/status", new { status = "Resolved", version = 2 })));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(Contenders - 1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await using var db = fixture.Database.CreateContext();
        Assert.Equal(1, await db.PendingNotifications.CountAsync(n => n.TicketId == ticket.Id()));
        Assert.Equal(3, (await db.Tickets.SingleAsync(t => t.Id == ticket.Id())).Version);
    }

    [IntegrationFact]
    public async Task StudentEditRacingATechnician_OnlyOneSucceeds()
    {
        var student = await fixture.ClientForAsync(fixture.Student1);
        var technician = await fixture.ClientForAsync(fixture.Technician);
        var ticket = await student.CreateTicketAsync();

        var responses = await Task.WhenAll(
            student.PatchAsJsonAsync($"/tickets/{ticket.Id()}", new { room = "Raced", version = 1 }),
            technician.PatchAsJsonAsync($"/tickets/{ticket.Id()}/status", new { status = "InProgress", version = 1 }));

        Assert.Equal(
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            responses.Select(response => response.StatusCode).Order());
        var current = await (await student.GetAsync($"/tickets/{ticket.Id()}")).ReadJsonAsync();
        Assert.Equal(2, current.Version());
    }
}
