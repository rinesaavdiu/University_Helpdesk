using System.ComponentModel.DataAnnotations;
using System.Text;

namespace UniversityHelpdesk.Api.Authentication;

public class JwtOptions : IValidatableObject
{
    public const string SectionName = "Jwt";

    public const int MinimumSigningKeyBytes = 32;

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    [Required(ErrorMessage = "Jwt:SigningKey is not configured. Set it with 'dotnet user-secrets' or the 'Jwt__SigningKey' environment variable.")]
    public string SigningKey { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(SigningKey) && Encoding.UTF8.GetByteCount(SigningKey) < MinimumSigningKeyBytes)
        {
            yield return new ValidationResult(
                $"Jwt:SigningKey must be at least {MinimumSigningKeyBytes} bytes long.",
                [nameof(SigningKey)]);
        }
    }
}
