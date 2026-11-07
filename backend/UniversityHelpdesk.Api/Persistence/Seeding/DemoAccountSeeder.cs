using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversityHelpdesk.Api.Authentication;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Persistence.Seeding;

public class DemoAccountSeeder(
    HelpdeskDbContext db,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<DemoAccountSeeder> logger)
{
    public const string CommandName = "seed-demo-users";

    private record DemoAccount(string Email, string DisplayName, UserRole Role, string PasswordKey);

    private static readonly DemoAccount[] Accounts =
    [
        new("student1@uni.test", "Demo Student One", UserRole.Student, "DemoAccounts:Student1Password"),
        new("student2@uni.test", "Demo Student Two", UserRole.Student, "DemoAccounts:Student2Password"),
        new("technician1@uni.test", "Demo Technician One", UserRole.Technician, "DemoAccounts:Technician1Password"),
    ];

    /// <summary>
    /// Creates missing demo accounts. Existing accounts are left unchanged, including their
    /// passwords and roles. Returns false if a password needed for a new account is missing.
    /// </summary>
    public async Task<bool> SeedAsync(CancellationToken cancellationToken = default)
    {
        var emails = Accounts.Select(account => EmailNormalizer.Normalize(account.Email)).ToList();
        var existing = await db.Users
            .Where(user => emails.Contains(user.Email))
            .Select(user => user.Email)
            .ToListAsync(cancellationToken);

        var missing = Accounts
            .Where(account => !existing.Contains(EmailNormalizer.Normalize(account.Email)))
            .ToList();

        var missingPasswords = missing
            .Where(account => string.IsNullOrWhiteSpace(configuration[account.PasswordKey]))
            .Select(account => account.PasswordKey)
            .ToList();

        if (missingPasswords.Count > 0)
        {
            logger.LogError(
                "Demo account passwords are not configured: {Keys}. Nothing was changed.",
                string.Join(", ", missingPasswords));
            return false;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var account in missing)
        {
            var user = new User
            {
                Email = EmailNormalizer.Normalize(account.Email),
                DisplayName = account.DisplayName,
                Role = account.Role,
                PasswordHash = "",
                CreatedAt = now,
            };
            user.PasswordHash = passwordHasher.HashPassword(user, configuration[account.PasswordKey]!);
            db.Users.Add(user);
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var account in Accounts)
        {
            var created = missing.Contains(account);
            logger.LogInformation(
                "{Email} ({Role}): {Outcome}",
                account.Email,
                account.Role,
                created ? "created" : "already exists, left unchanged");
        }

        return true;
    }
}
