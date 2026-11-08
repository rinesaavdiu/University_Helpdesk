namespace UniversityHelpdesk.Benchmark;

public sealed record BenchmarkOptions(
    Uri BaseUrl,
    string DatabaseConnectionString,
    string? Redis,
    int Users,
    int Tickets,
    int ListRequestsPerUser,
    int CategoryRequestsPerUser,
    int ColdReads,
    string? Environment,
    string? OutputPath)
{
    public const string DatabaseVariable = "HELPDESK_BENCH_DATABASE";
    public const string RedisVariable = "HELPDESK_BENCH_REDIS";

    public const string Usage = $"""
        Usage: dotnet run --project benchmarks/UniversityHelpdesk.Benchmark -c Release -- [options]

          --base-url <url>            API address (default http://localhost:58080)
          --users <n>                 concurrent users (default 20)
          --tickets <n>               tickets created by the benchmark users (default 1000)
          --list-requests <n>         GET /tickets requests per user and listing scenario (default 50)
          --category-requests <n>     warm GET /categories requests per user (default 50)
          --cold-reads <n>            cold GET /categories requests, run one at a time (default 50)
          --redis <configuration>     Redis used by the API; needed for cold category reads
          --environment <text>        description of the deployment, stored with the results
          --output <file.json>        also write the results as JSON

        Environment:
          {DatabaseVariable}     connection string of the API's database (required); the
                                      benchmark creates its own accounts there and removes them
          {RedisVariable}        alternative to --redis
        """;

    public static BenchmarkOptions? Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                return null;
            }

            values[args[i][2..]] = args[i + 1];
        }

        var database = System.Environment.GetEnvironmentVariable(DatabaseVariable);
        if (string.IsNullOrWhiteSpace(database))
        {
            Console.Error.WriteLine($"{DatabaseVariable} is not set.");
            return null;
        }

        try
        {
            return new BenchmarkOptions(
                new Uri(values.GetValueOrDefault("base-url", "http://localhost:58080")),
                database,
                values.GetValueOrDefault("redis") ?? System.Environment.GetEnvironmentVariable(RedisVariable),
                Positive(values, "users", 20),
                Positive(values, "tickets", 1000),
                Positive(values, "list-requests", 50),
                Positive(values, "category-requests", 50),
                Positive(values, "cold-reads", 50),
                values.GetValueOrDefault("environment"),
                values.GetValueOrDefault("output"));
        }
        catch (FormatException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return null;
        }
    }

    private static int Positive(Dictionary<string, string> values, string name, int fallback)
    {
        if (!values.TryGetValue(name, out var text))
        {
            return fallback;
        }

        return int.TryParse(text, out var value) && value > 0
            ? value
            : throw new FormatException($"--{name} must be a positive whole number.");
    }
}
