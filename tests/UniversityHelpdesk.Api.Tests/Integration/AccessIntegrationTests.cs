using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace UniversityHelpdesk.Api.Tests.Integration;

/// <summary>Login, ownership, and role rules against the real database.</summary>
[Collection(ApiIntegrationCollection.Name)]
public class AccessIntegrationTests(ApiIntegrationFixture fixture)
{
    [IntegrationFact]
    public async Task Login_WithValidCredentials_ReturnsAWorkingTokenForThatUser()
    {
        var account = fixture.Student1;
        var response = await fixture.Anonymous().PostAsJsonAsync("/auth/login",
            new { email = account.Email.ToUpperInvariant(), password = account.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.Equal(3600, body.GetProperty("expiresIn").GetInt32());
        var user = body.GetProperty("user");
        Assert.Equal(account.Id, user.GetProperty("id").GetInt32());
        Assert.Equal(account.Email, user.GetProperty("email").GetString());
        Assert.Equal("Student", user.GetProperty("role").GetString());

        var client = fixture.Anonymous();
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/tickets")).StatusCode);
    }

    [IntegrationFact]
    public async Task Login_WithWrongPasswordOrUnknownEmail_Returns401WithTheSameMessage()
    {
        var client = fixture.Anonymous();
        var wrongPassword = await client.PostAsJsonAsync("/auth/login",
            new { email = fixture.Student1.Email, password = "not-the-password" });
        var unknownEmail = await client.PostAsJsonAsync("/auth/login",
            new { email = $"nobody-{fixture.Database.RunId}@integration.test", password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(
            (await wrongPassword.ReadJsonAsync()).GetProperty("detail").GetString(),
            (await unknownEmail.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    [IntegrationFact]
    public async Task Categories_ListOnlyActiveCategories_OrderedByName()
    {
        var client = await fixture.ClientForAsync(fixture.Student1);

        var categories = await (await client.GetAsync("/categories")).ReadJsonAsync();

        var names = categories.EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToList();
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.Contains("Projector", names);
        Assert.DoesNotContain(categories.EnumerateArray(), c => c.GetProperty("id").GetInt32() == TestDatabase.InactiveCategoryId);
    }

    [IntegrationFact]
    public async Task AnotherStudentsTicket_IsNotFound_AndStaysUnchanged()
    {
        var owner = await fixture.ClientForAsync(fixture.Student1);
        var other = await fixture.ClientForAsync(fixture.Student2);
        var ticket = await owner.CreateTicketAsync();
        var path = $"/tickets/{ticket.Id()}";

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync(path, new { room = "Z-999", version = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(path)).StatusCode);
        var otherList = await (await other.GetAsync("/tickets?pageSize=100")).ReadJsonAsync();
        Assert.DoesNotContain(otherList.GetProperty("items").EnumerateArray(), item => item.Id() == ticket.Id());

        var unchanged = await (await owner.GetAsync(path)).ReadJsonAsync();
        Assert.Equal("B-101", unchanged.GetProperty("room").GetString());
        Assert.Equal(1, unchanged.Version());
    }

    [IntegrationFact]
    public async Task Technician_SeesEveryStudentsTickets()
    {
        var first = await (await fixture.ClientForAsync(fixture.Student1)).CreateTicketAsync();
        var second = await (await fixture.ClientForAsync(fixture.Student2)).CreateTicketAsync();
        var technician = await fixture.ClientForAsync(fixture.Technician);

        Assert.Equal(HttpStatusCode.OK, (await technician.GetAsync($"/tickets/{first.Id()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await technician.GetAsync($"/tickets/{second.Id()}")).StatusCode);
        var page = await (await technician.GetAsync("/tickets?pageSize=100")).ReadJsonAsync();
        var ids = page.GetProperty("items").EnumerateArray().Select(item => item.Id()).ToList();
        Assert.Contains(first.Id(), ids);
        Assert.Contains(second.Id(), ids);
    }

    [IntegrationFact]
    public async Task RoleRestrictions_AreEnforced()
    {
        var student = await fixture.ClientForAsync(fixture.Student1);
        var technician = await fixture.ClientForAsync(fixture.Technician);
        var ticket = await student.CreateTicketAsync();
        var path = $"/tickets/{ticket.Id()}";

        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsJsonAsync("/tickets",
            new { room = "B-101", categoryId = 3, description = "Technicians cannot report tickets." })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PatchAsJsonAsync(path, new { room = "Z-1", version = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.DeleteAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await student.PatchAsJsonAsync($"{path}/status", new { status = "InProgress", version = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Anonymous().GetAsync(path)).StatusCode);

        var unchanged = await (await student.GetAsync(path)).ReadJsonAsync();
        Assert.Equal("Open", unchanged.GetProperty("status").GetString());
        Assert.Equal(1, unchanged.Version());
    }
}
