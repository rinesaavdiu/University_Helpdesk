using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using UniversityHelpdesk.Api.Domain;
using UniversityHelpdesk.Api.Persistence;

namespace UniversityHelpdesk.Api.Tests;

public class HelpdeskModelTests
{
    // Building the model never opens a connection, so these tests need no database.
    private static HelpdeskDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HelpdeskDbContext>()
            .UseNpgsql("Host=unused")
            .Options);

    [Fact]
    public void TicketVersion_IsConcurrencyToken()
    {
        using var context = CreateContext();

        var version = context.Model.FindEntityType(typeof(Ticket))!.FindProperty(nameof(Ticket.Version))!;

        Assert.True(version.IsConcurrencyToken);
    }

    [Fact]
    public void Categories_AreSeededWithStableIds()
    {
        using var context = CreateContext();

        var seeded = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Category))!
            .GetSeedData()
            .Select(row => ((int)row[nameof(Category.Id)]!, (string)row[nameof(Category.Name)]!, (bool)row[nameof(Category.IsActive)]!))
            .OrderBy(row => row.Item1)
            .ToList();

        Assert.Equal(
            [
                (1, "Audio", true),
                (2, "Computer", true),
                (3, "Projector", true),
                (4, "Network", true),
                (5, "Smart Board", true),
                (6, "Other", true),
            ],
            seeded);
    }
}
