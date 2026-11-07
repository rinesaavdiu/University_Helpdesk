namespace UniversityHelpdesk.Api.Authentication;

public static class EmailNormalizer
{
    // Must match the CK_Users_Email_Lowercase database constraint.
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
