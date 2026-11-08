using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversityHelpdesk.Api.Domain;
using UniversityHelpdesk.Api.Persistence;

namespace UniversityHelpdesk.Api.Tests.Integration;

public record TestAccount(int Id, string Email, string Password, UserRole Role);

/// <summary>
/// Access to the dedicated test database. Each run creates its own accounts, whose emails carry a
/// run ID, and later deletes them with their tickets and notifications. Other data, including
/// that of runs in progress, is never touched, so runs can be repeated or overlap.
/// </summary>
public sealed class TestDatabase(string connectionString, string suite)
{
    /// <summary>Category kept inactive so tests can check that it is rejected and not listed.</summary>
    public const int InactiveCategoryId = 1000;

    public string ConnectionString { get; } = connectionString;

    public string RunId { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));

    private string EmailDomain => $"{suite}.test";

    public HelpdeskDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HelpdeskDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>Applies migrations and removes accounts left behind by earlier runs of this suite that did not finish.</summary>
    public async Task PrepareAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Categories" ("Id", "Name", "IsActive")
            VALUES ({InactiveCategoryId}, 'Retired (test only)', false)
            ON CONFLICT ("Id") DO UPDATE SET "IsActive" = false
            """);

        var staleBefore = DateTime.UtcNow.AddHours(-1);
        var domain = "%@" + EmailDomain;
        await DeleteAccountsAsync(db, db.Users.Where(user => EF.Functions.Like(user.Email, domain) && user.CreatedAt < staleBefore));
    }

    public async Task<TestAccount> CreateAccountAsync(string name, UserRole role)
    {
        var email = $"{name}-{RunId}@{EmailDomain}";
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var user = new User
        {
            Email = email,
            DisplayName = $"Test {name}",
            Role = role,
            PasswordHash = "",
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);

        await using var db = CreateContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new TestAccount(user.Id, email, password, role);
    }

    /// <summary>Deletes this run's accounts, tickets, and notifications.</summary>
    public async Task CleanUpAsync()
    {
        await using var db = CreateContext();
        var domain = $"%-{RunId}@{EmailDomain}";
        await DeleteAccountsAsync(db, db.Users.Where(user => EF.Functions.Like(user.Email, domain)));
    }

    private static async Task DeleteAccountsAsync(HelpdeskDbContext db, IQueryable<User> users)
    {
        var ids = await users.Select(user => user.Id).ToListAsync();
        if (ids.Count == 0)
        {
            return;
        }

        await db.PendingNotifications.Where(n => ids.Contains(n.Ticket.StudentId)).ExecuteDeleteAsync();
        await db.Tickets.Where(ticket => ids.Contains(ticket.StudentId)).ExecuteDeleteAsync();
        await db.Users.Where(user => ids.Contains(user.Id)).ExecuteDeleteAsync();
    }
}
