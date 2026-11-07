using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversityHelpdesk.Api.Domain;
using UniversityHelpdesk.Api.Persistence;

namespace UniversityHelpdesk.Api.Authentication;

public record LoginResult(User User, AccessToken Token);

public class LoginService(HelpdeskDbContext db, IPasswordHasher<User> passwordHasher, TokenService tokenService)
{
    // Unknown emails are checked against this hash so they take about as long as a wrong password.
    private static readonly Lazy<string> UnknownUserHash = new(() =>
        new PasswordHasher<User>().HashPassword(null!, Guid.NewGuid().ToString()));

    /// <summary>Returns null when the email is unknown or the password is wrong.</summary>
    public async Task<LoginResult?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(email);
        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(null!, UnknownUserHash.Value, password);
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return new LoginResult(user, tokenService.CreateToken(user));
    }
}
