using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Tests.Integration;

/// <summary>The ordinary test host, connected to the dedicated test database.</summary>
public class DatabaseApiFactory(string connectionString) : HelpdeskApiFactory
{
    protected override IDictionary<string, string?> ExtraSettings => new Dictionary<string, string?>
    {
        ["ConnectionStrings:HelpdeskDatabase"] = connectionString,
    };
}

[CollectionDefinition(Name)]
public class ApiIntegrationCollection : ICollectionFixture<ApiIntegrationFixture>
{
    public const string Name = "API integration";
}

/// <summary>
/// One API host and one set of accounts for the whole collection. Each test creates the tickets
/// it checks, so tests do not depend on each other's data.
/// </summary>
public sealed class ApiIntegrationFixture : IAsyncLifetime
{
    private readonly Dictionary<string, string> _tokens = [];

    public TestDatabase Database { get; private set; } = null!;
    public DatabaseApiFactory Factory { get; private set; } = null!;
    public TestAccount Student1 { get; private set; } = null!;
    public TestAccount Student2 { get; private set; } = null!;
    public TestAccount Technician { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (IntegrationSettings.Database is null)
        {
            return;
        }

        Database = new TestDatabase(IntegrationSettings.RequireTestDatabase(), "integration");
        await Database.PrepareAsync();
        Student1 = await Database.CreateAccountAsync("student1", UserRole.Student);
        Student2 = await Database.CreateAccountAsync("student2", UserRole.Student);
        Technician = await Database.CreateAccountAsync("technician", UserRole.Technician);
        Factory = new DatabaseApiFactory(Database.ConnectionString);
    }

    public HttpClient Anonymous() => Factory.CreateClient();

    public async Task<HttpClient> ClientForAsync(TestAccount account)
    {
        if (!_tokens.TryGetValue(account.Email, out var token))
        {
            var response = await Anonymous().PostAsJsonAsync("/auth/login", new { email = account.Email, password = account.Password });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            token = (await response.ReadJsonAsync()).GetProperty("accessToken").GetString()!;
            _tokens[account.Email] = token;
        }

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task DisposeAsync()
    {
        if (Database is null)
        {
            return;
        }

        await Factory.DisposeAsync();
        await Database.CleanUpAsync();
    }
}

public static class HttpTestExtensions
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string path, T body) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, path) { Content = JsonContent.Create(body) });

    public static async Task<JsonElement> CreateTicketAsync(
        this HttpClient client, string room = "B-101", int categoryId = 3, string description = "The projector shows no image from HDMI.")
    {
        var response = await client.PostAsJsonAsync("/tickets", new { room, categoryId, description });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    public static async Task<JsonElement> ChangeStatusAsync(this HttpClient client, int ticketId, string status, int version)
    {
        var response = await client.PatchAsJsonAsync($"/tickets/{ticketId}/status", new { status, version });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    public static int Id(this JsonElement ticket) => ticket.GetProperty("id").GetInt32();

    public static int Version(this JsonElement ticket) => ticket.GetProperty("version").GetInt32();
}
