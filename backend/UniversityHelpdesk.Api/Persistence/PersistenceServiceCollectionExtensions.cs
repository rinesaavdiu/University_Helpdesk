using Microsoft.EntityFrameworkCore;

namespace UniversityHelpdesk.Api.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "HelpdeskDatabase";

    public static IServiceCollection AddHelpdeskPersistence(this IServiceCollection services)
    {
        // The connection string is read when a context is first created, so the API
        // can start and serve Swagger without database configuration.
        services.AddDbContext<HelpdeskDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionString(ConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"The connection string '{ConnectionStringName}' is not configured. " +
                    $"Set the environment variable 'ConnectionStrings__{ConnectionStringName}' " +
                    "to a PostgreSQL connection string.");
            }

            options.UseNpgsql(connectionString);
        });

        return services;
    }
}
