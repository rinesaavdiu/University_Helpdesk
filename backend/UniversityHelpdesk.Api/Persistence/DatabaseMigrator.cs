using Microsoft.EntityFrameworkCore;

namespace UniversityHelpdesk.Api.Persistence;

/// <summary>
/// Applies pending EF Core migrations when the API is started with <see cref="CommandName"/>.
/// The API never migrates on normal startup, so replicas do not race to change the schema.
/// </summary>
public class DatabaseMigrator(HelpdeskDbContext db, ILogger<DatabaseMigrator> logger)
{
    public const string CommandName = "migrate-database";

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("The database schema is up to date; no migrations to apply.");
            return;
        }

        logger.LogInformation("Applying {Count} migrations: {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Applied {Count} migrations.", pending.Count);
    }
}
