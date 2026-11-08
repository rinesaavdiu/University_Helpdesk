using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversityHelpdesk.Api.Domain;
using UniversityHelpdesk.Api.Persistence;

namespace UniversityHelpdesk.Benchmark;

public record BenchmarkAccount(string Email, string Password, UserRole Role);

/// <summary>
/// The benchmark's own accounts. Their emails carry the run ID and the <c>benchmark.test</c>
/// domain; cleanup deletes only those accounts and the tickets they created.
/// </summary>
public sealed class BenchmarkData(string connectionString)
{
    private const string EmailDomain = "benchmark.test";

    public string RunId { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));

    /// <summary>Removes accounts left by earlier benchmark runs that were interrupted before cleanup.</summary>
    public async Task<int> RemoveStaleAsync()
    {
        await using var db = CreateContext();
        var staleBefore = DateTime.UtcNow.AddHours(-2);
        return await DeleteAccountsAsync(db, db.Users.Where(user =>
            EF.Functions.Like(user.Email, "%@" + EmailDomain) && user.CreatedAt < staleBefore));
    }

    public async Task<IReadOnlyList<BenchmarkAccount>> CreateAccountsAsync(int students)
    {
        var hasher = new PasswordHasher<User>();
        var accounts = new List<BenchmarkAccount>();
        var users = new List<User>();

        for (var i = 0; i <= students; i++)
        {
            var (name, role) = i == 0 ? ("technician", UserRole.Technician) : ($"student{i:00}", UserRole.Student);
            var email = $"{name}-{RunId}@{EmailDomain}";
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
            var user = new User
            {
                Email = email,
                DisplayName = $"Benchmark {name}",
                Role = role,
                PasswordHash = "",
                CreatedAt = DateTime.UtcNow,
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            users.Add(user);
            accounts.Add(new BenchmarkAccount(email, password, role));
        }

        await using var db = CreateContext();
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
        return accounts;
    }

    /// <summary>Deletes this run's accounts and everything that references them.</summary>
    public async Task<int> CleanUpAsync()
    {
        await using var db = CreateContext();
        var pattern = $"%-{RunId}@{EmailDomain}";
        return await DeleteAccountsAsync(db, db.Users.Where(user => EF.Functions.Like(user.Email, pattern)));
    }

    private HelpdeskDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HelpdeskDbContext>().UseNpgsql(connectionString).Options);

    private static async Task<int> DeleteAccountsAsync(HelpdeskDbContext db, IQueryable<User> users)
    {
        var ids = await users.Select(user => user.Id).ToListAsync();
        if (ids.Count == 0)
        {
            return 0;
        }

        await db.PendingNotifications.Where(n => ids.Contains(n.Ticket.StudentId)).ExecuteDeleteAsync();
        var tickets = await db.Tickets.Where(ticket => ids.Contains(ticket.StudentId)).ExecuteDeleteAsync();
        await db.Users.Where(user => ids.Contains(user.Id)).ExecuteDeleteAsync();
        return tickets;
    }
}
