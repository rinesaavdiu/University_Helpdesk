using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using StackExchange.Redis;
using UniversityHelpdesk.Api.Categories;
using UniversityHelpdesk.Api.Controllers;
using UniversityHelpdesk.Api.Tickets;

namespace UniversityHelpdesk.Benchmark;

public record BenchmarkReport(
    DateTimeOffset StartedAt,
    string Environment,
    string Client,
    string BaseUrl,
    int Users,
    int TicketsCreated,
    int TicketsVisibleToTechnician,
    IReadOnlyList<ScenarioResult> Scenarios,
    IReadOnlyList<string> Notes);

public static class BenchmarkRunner
{
    private const int ListPageSize = 20;
    private const int ListPages = 5;
    private const int WarmUpRequests = 10;

    public static async Task<int> RunAsync(BenchmarkOptions options)
    {
        var data = new BenchmarkData(options.DatabaseConnectionString);
        var stale = await data.RemoveStaleAsync();
        Console.WriteLine($"Run {data.RunId}: {options.Users} users, {options.Tickets} tickets, API {options.BaseUrl}");
        if (stale > 0)
        {
            Console.WriteLine($"Removed {stale} tickets left by interrupted benchmark runs.");
        }

        var startedAt = DateTimeOffset.UtcNow;
        var scenarios = new List<ScenarioResult>();
        var notes = new List<string>();
        using var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        {
            BaseAddress = options.BaseUrl,
            Timeout = TimeSpan.FromSeconds(30),
        };

        int created;
        int visible;
        try
        {
            var accounts = await data.CreateAccountsAsync(options.Users);
            var tokens = new string[accounts.Count];
            scenarios.Add(await Scenario.RunAsync("POST /auth/login", accounts.Count, 1, async (worker, _) =>
            {
                using var response = await http.PostAsJsonAsync(
                    "/auth/login", new { accounts[worker].Email, accounts[worker].Password });
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                tokens[worker] = (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonSerializerOptions.Web))!.AccessToken;
                return true;
            }));
            if (tokens.Any(string.IsNullOrEmpty))
            {
                Console.Error.WriteLine("Some benchmark accounts could not log in; stopping.");
                return 1;
            }

            var technician = tokens[0];
            var students = tokens[1..];
            var categories = await GetAsync<List<CategorySummary>>(http, "/categories", technician);
            if (categories is not { Count: > 0 })
            {
                Console.Error.WriteLine("GET /categories returned no active category; stopping.");
                return 1;
            }

            for (var i = 0; i < WarmUpRequests; i++)
            {
                await SendAsync(http, HttpMethod.Get, "/tickets", technician);
                await SendAsync(http, HttpMethod.Get, "/categories", technician);
            }

            // Every user creates an equal share; the first users take the remainder.
            var perUser = options.Tickets / options.Users;
            var remainder = options.Tickets % options.Users;
            var createdCount = 0;
            scenarios.Add(await Scenario.RunAsync("POST /tickets", options.Users, worker => perUser + (worker < remainder ? 1 : 0), async (worker, i) =>
            {
                using var response = await SendAsync(http, HttpMethod.Post, "/tickets", students[worker], new
                {
                    Room = $"B-{worker:00}{i:000}",
                    CategoryId = categories[(worker + i) % categories.Count].Id,
                    Description = $"Benchmark ticket {i} from user {worker} in run {data.RunId}.",
                });
                if (response.StatusCode != HttpStatusCode.Created)
                {
                    return false;
                }

                Interlocked.Increment(ref createdCount);
                return true;
            }));

            created = createdCount;
            visible = (await GetAsync<TicketPage>(http, "/tickets?pageSize=1", technician))!.TotalCount;

            scenarios.Add(await Scenario.RunAsync("GET /tickets (technician, all tickets)", options.Users, options.ListRequestsPerUser,
                (worker, i) => ExpectOkAsync(http, $"/tickets?page={(worker + i) % ListPages + 1}&pageSize={ListPageSize}", technician)));

            scenarios.Add(await Scenario.RunAsync("GET /tickets (student, own tickets)", options.Users, options.ListRequestsPerUser,
                (worker, i) => ExpectOkAsync(http, $"/tickets?page={i % 3 + 1}&pageSize={ListPageSize}", students[worker])));

            if (string.IsNullOrWhiteSpace(options.Redis))
            {
                notes.Add("Cold GET /categories was skipped because no Redis configuration was given.");
            }
            else
            {
                await using var redis = await ConnectionMultiplexer.ConnectAsync(options.Redis);
                var cache = redis.GetDatabase();
                scenarios.Add(await Scenario.RunAsync("GET /categories (cold: cache key deleted first)", 1, options.ColdReads,
                    async (_, _) =>
                    {
                        await cache.KeyDeleteAsync(CategoryCache.Key);
                        return await ExpectOkAsync(http, "/categories", technician);
                    }));
                notes.Add("Cold category reads time only the HTTP request; the Redis DEL before each one is not counted.");
            }

            await SendAsync(http, HttpMethod.Get, "/categories", technician);
            scenarios.Add(await Scenario.RunAsync("GET /categories (warm)", options.Users, options.CategoryRequestsPerUser,
                (worker, _) => ExpectOkAsync(http, "/categories", students[worker])));
        }
        finally
        {
            var removed = await data.CleanUpAsync();
            Console.WriteLine($"Cleanup removed the run's accounts and {removed} tickets.");
        }

        var report = new BenchmarkReport(
            startedAt,
            options.Environment ?? "not specified",
            $"{RuntimeInformation.OSDescription}, {System.Environment.ProcessorCount} logical CPUs, .NET {System.Environment.Version}",
            options.BaseUrl.ToString(),
            options.Users,
            created,
            visible,
            scenarios,
            notes);
        Print(report);

        if (options.OutputPath is not null)
        {
            await File.WriteAllTextAsync(options.OutputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerOptions.Web) { WriteIndented = true }));
            Console.WriteLine($"Results written to {options.OutputPath}");
        }

        return scenarios.Any(scenario => scenario.Failures > 0) ? 1 : 0;
    }

    private static void Print(BenchmarkReport report)
    {
        Console.WriteLine();
        Console.WriteLine($"Started:     {report.StartedAt:u}");
        Console.WriteLine($"Environment: {report.Environment}");
        Console.WriteLine($"Client:      {report.Client}");
        Console.WriteLine($"Dataset:     {report.TicketsCreated} tickets created by {report.Users} users; {report.TicketsVisibleToTechnician} tickets visible to the technician");
        Console.WriteLine();
        Console.WriteLine("| Scenario | Concurrency | Requests | Failures | Median (ms) | p95 (ms) | Max (ms) | Requests/s |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var s in report.Scenarios)
        {
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"| {s.Name} | {s.Concurrency} | {s.Requests} | {s.Failures} | {s.MedianMs:F1} | {s.P95Ms:F1} | {s.MaxMs:F1} | {s.RequestsPerSecond:F0} |"));
        }

        foreach (var note in report.Notes)
        {
            Console.WriteLine($"Note: {note}");
        }
    }

    private static async Task<bool> ExpectOkAsync(HttpClient http, string path, string token)
    {
        using var response = await SendAsync(http, HttpMethod.Get, path, token);
        await response.Content.LoadIntoBufferAsync();
        return response.StatusCode == HttpStatusCode.OK;
    }

    private static async Task<T?> GetAsync<T>(HttpClient http, string path, string token)
    {
        using var response = await SendAsync(http, HttpMethod.Get, path, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonSerializerOptions.Web);
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient http, HttpMethod method, string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonSerializerOptions.Web);
        }

        return http.SendAsync(request);
    }
}
