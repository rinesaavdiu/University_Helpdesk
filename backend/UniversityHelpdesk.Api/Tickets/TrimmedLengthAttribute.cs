using System.ComponentModel.DataAnnotations;

namespace UniversityHelpdesk.Api.Tickets;

/// <summary>Checks a string's length after trimming leading and trailing whitespace.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public class TrimmedLengthAttribute(int minimum, int maximum) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        var length = ((string)value).Trim().Length;
        return length >= minimum && length <= maximum
            ? ValidationResult.Success
            : new ValidationResult(
                $"The {validationContext.DisplayName} field must be between {minimum} and {maximum} characters after trimming.");
    }
}
