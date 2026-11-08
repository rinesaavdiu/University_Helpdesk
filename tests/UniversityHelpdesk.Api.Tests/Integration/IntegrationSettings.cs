using Npgsql;

namespace UniversityHelpdesk.Api.Tests.Integration;

/// <summary>
/// Settings for the tests that need real services. They are read from environment variables, so
/// an ordinary <c>dotnet test</c> without them skips those tests instead of failing.
/// </summary>
public static class IntegrationSettings
{
    public const string DatabaseVariable = "HELPDESK_TEST_DATABASE";

    /// <summary>Connection string of a dedicated test database, or null when not configured.</summary>
    public static string? Database => Read(DatabaseVariable);

    public static string? Read(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// The tests create and delete data, so they refuse to run against a database that is not
    /// clearly meant for tests.
    /// </summary>
    public static string RequireTestDatabase()
    {
        var connectionString = Database
            ?? throw new InvalidOperationException($"{DatabaseVariable} is not set.");
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database;
        if (name is null || !name.EndsWith("_test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{DatabaseVariable} must point at a dedicated database whose name ends in '_test'.");
        }

        return connectionString;
    }
}

/// <summary>A test that runs only when <see cref="IntegrationSettings.DatabaseVariable"/> is set.</summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (IntegrationSettings.Database is null)
        {
            Skip = $"Set {IntegrationSettings.DatabaseVariable} to run the database integration tests.";
        }
    }
}

/// <inheritdoc cref="IntegrationFactAttribute"/>
public sealed class IntegrationTheoryAttribute : TheoryAttribute
{
    public IntegrationTheoryAttribute()
    {
        if (IntegrationSettings.Database is null)
        {
            Skip = $"Set {IntegrationSettings.DatabaseVariable} to run the database integration tests.";
        }
    }
}
