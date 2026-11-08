using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UniversityHelpdesk.Api.Tests.Integration;

/// <summary>Create, read, update, delete, validation, and pagination against the real database.</summary>
[Collection(ApiIntegrationCollection.Name)]
public class TicketCrudIntegrationTests(ApiIntegrationFixture fixture)
{
    [IntegrationFact]
    public async Task Ticket_CanBeCreatedReadUpdatedAndDeleted()
    {
        var student = await fixture.ClientForAsync(fixture.Student1);

        var createResponse = await student.PostAsJsonAsync("/tickets",
            new { room = "A-204", categoryId = 3, description = "The projector turns on but shows no image." });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.ReadJsonAsync();
        Assert.Equal($"/tickets/{created.Id()}", createResponse.Headers.Location?.OriginalString);
        Assert.Equal(fixture.Student1.Id, created.GetProperty("studentId").GetInt32());
        Assert.Equal("Open", created.GetProperty("status").GetString());
        Assert.Equal(1, created.Version());
        Assert.Equal("Projector", created.GetProperty("category").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("resolvedAt").ValueKind);

        var read = await (await student.GetAsync(createResponse.Headers.Location)).ReadJsonAsync();
        Assert.Equal(created.ToString(), read.ToString());

        var updateResponse = await student.PatchAsJsonAsync($"/tickets/{created.Id()}",
            new { room = "A-206", categoryId = 2, version = 1 });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.ReadJsonAsync();
        Assert.Equal("A-206", updated.GetProperty("room").GetString());
        Assert.Equal("Computer", updated.GetProperty("category").GetProperty("name").GetString());
        Assert.Equal(created.GetProperty("description").GetString(), updated.GetProperty("description").GetString());
        Assert.Equal(2, updated.Version());
        Assert.True(updated.GetProperty("updatedAt").GetDateTime() >= created.GetProperty("updatedAt").GetDateTime());

        var deleteResponse = await student.DeleteAsync($"/tickets/{created.Id()}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty(await deleteResponse.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync($"/tickets/{created.Id()}")).StatusCode);

        await using var db = fixture.Database.CreateContext();
        Assert.False(db.Tickets.Any(ticket => ticket.Id == created.Id()));
    }

    [IntegrationFact]
    public async Task Update_WithAStaleVersion_Returns409AndKeepsTheTicket()
    {
        var student = await fixture.ClientForAsync(fixture.Student1);
        var ticket = await student.CreateTicketAsync();
        await student.PatchAsJsonAsync($"/tickets/{ticket.Id()}", new { room = "C-1", version = 1 });

        var stale = await student.PatchAsJsonAsync($"/tickets/{ticket.Id()}", new { room = "C-2", version = 1 });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var current = await (await student.GetAsync($"/tickets/{ticket.Id()}")).ReadJsonAsync();
        Assert.Equal("C-1", current.GetProperty("room").GetString());
        Assert.Equal(2, current.Version());
    }

    [IntegrationTheory]
    [InlineData(TestDatabase.InactiveCategoryId)]
    [InlineData(999_999)]
    public async Task InactiveOrUnknownCategory_IsRejectedOnCreateAndEdit(int categoryId)
    {
        var student = await fixture.ClientForAsync(fixture.Student1);
        var ticket = await student.CreateTicketAsync();

        var create = await student.PostAsJsonAsync("/tickets",
            new { room = "B-101", categoryId, description = "A ticket with an unusable category." });
        var edit = await student.PatchAsJsonAsync($"/tickets/{ticket.Id()}", new { categoryId, version = 1 });

        foreach (var response in new[] { create, edit })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True((await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("categoryId", out _));
        }
    }

    [IntegrationTheory]
    [InlineData("""{"room":"","categoryId":3,"description":"Long enough description."}""", "room")]
    [InlineData("""{"room":"B-101","categoryId":3,"description":"short"}""", "description")]
    [InlineData("""{"room":"B-101","categoryId":3,"description":"Long enough description.","status":"Resolved"}""", "status")]
    [InlineData("""{"room":"B-101","categoryId":"three","description":"Long enough description."}""", "categoryId")]
    public async Task InvalidCreateRequest_Returns400NamingTheField_AndSavesNothing(string body, string field)
    {
        var student = await fixture.ClientForAsync(fixture.Student1);
        await using var db = fixture.Database.CreateContext();
        var before = db.Tickets.Count(ticket => ticket.StudentId == fixture.Student1.Id);

        var response = await student.PostAsync("/tickets", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty(field, out _));
        Assert.Equal(before, db.Tickets.Count(ticket => ticket.StudentId == fixture.Student1.Id));
    }

    [IntegrationFact]
    public async Task MissingTicket_Returns404ForEveryAction()
    {
        var student = await fixture.ClientForAsync(fixture.Student1);
        var technician = await fixture.ClientForAsync(fixture.Technician);
        const string path = "/tickets/2147483000";

        Assert.Equal(HttpStatusCode.NotFound, (await student.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.PatchAsJsonAsync(path, new { room = "X", version = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await student.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await technician.PatchAsJsonAsync($"{path}/status", new { status = "InProgress", version = 1 })).StatusCode);
    }

    [IntegrationFact]
    public async Task Pagination_PagesThroughOwnTickets_NewestFirst()
    {
        var pager = await fixture.Database.CreateAccountAsync("pager", Domain.UserRole.Student);
        var student = await fixture.ClientForAsync(pager);
        var created = new List<int>();
        for (var i = 1; i <= 5; i++)
        {
            created.Add((await student.CreateTicketAsync(room: $"P-{i}")).Id());
        }

        var newestFirst = Enumerable.Reverse(created).ToList();
        var seen = new List<int>();
        foreach (var (page, expectedCount) in new[] { (1, 2), (2, 2), (3, 1) })
        {
            var body = await (await student.GetAsync($"/tickets?page={page}&pageSize=2")).ReadJsonAsync();
            Assert.Equal(page, body.GetProperty("page").GetInt32());
            Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
            Assert.Equal(5, body.GetProperty("totalCount").GetInt32());
            var items = body.GetProperty("items").EnumerateArray().Select(item => item.Id()).ToList();
            Assert.Equal(expectedCount, items.Count);
            seen.AddRange(items);
        }

        Assert.Equal(newestFirst, seen);
        var beyond = await (await student.GetAsync("/tickets?page=4&pageSize=2")).ReadJsonAsync();
        Assert.Equal(0, beyond.GetProperty("items").GetArrayLength());
        Assert.Equal(5, beyond.GetProperty("totalCount").GetInt32());

        var technician = await fixture.ClientForAsync(fixture.Technician);
        var moved = await technician.ChangeStatusAsync(created[1], "InProgress", 1);
        var inProgress = await (await student.GetAsync("/tickets?status=InProgress")).ReadJsonAsync();
        Assert.Equal(new[] { moved.Id() }, inProgress.GetProperty("items").EnumerateArray().Select(item => item.Id()));
        var open = await (await student.GetAsync("/tickets?status=Open")).ReadJsonAsync();
        Assert.Equal(4, open.GetProperty("totalCount").GetInt32());
    }

    [IntegrationTheory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("status=Closed")]
    public async Task InvalidListQuery_Returns400(string query)
    {
        var student = await fixture.ClientForAsync(fixture.Student1);

        Assert.Equal(HttpStatusCode.BadRequest, (await student.GetAsync($"/tickets?{query}")).StatusCode);
    }
}
