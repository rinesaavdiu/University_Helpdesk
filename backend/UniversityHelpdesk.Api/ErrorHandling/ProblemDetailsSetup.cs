using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;

namespace UniversityHelpdesk.Api.ErrorHandling;

public static partial class ProblemDetailsSetup
{
    public const string ValidationDetail = "One or more fields are invalid.";
    public const string UnexpectedErrorDetail = "An unexpected error occurred. Use the traceId when reporting this problem.";

    /// <summary>
    /// Registers controllers with the contract's error behavior: every error body is Problem
    /// Details with a status-based type, a standard title, and a traceId; unknown JSON fields
    /// are rejected; and validation errors are keyed by JSON field name.
    /// </summary>
    public static IServiceCollection AddHelpdeskControllers(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            var status = problem.Status ?? context.HttpContext.Response.StatusCode;
            problem.Status = status;
            problem.Type = $"https://httpstatuses.io/{status}";
            problem.Title = ReasonPhrases.GetReasonPhrase(status);
            problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        });

        services.AddExceptionHandler<UnexpectedExceptionHandler>();

        services
            .AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = CreateValidationResponse);

        return services;
    }

    private static IActionResult CreateValidationResponse(ActionContext context)
    {
        var sanitized = new ModelStateDictionary();
        foreach (var (field, messages) in SanitizeErrors(context.ModelState))
        {
            foreach (var message in messages)
            {
                sanitized.AddModelError(field, message);
            }
        }

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(
            context.HttpContext, sanitized, StatusCodes.Status400BadRequest, detail: ValidationDetail);

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    /// <summary>
    /// Keeps field validation messages, but replaces JSON parser messages, which can echo parts
    /// of the request body or internal type names, with fixed messages.
    /// </summary>
    private static Dictionary<string, string[]> SanitizeErrors(ModelStateDictionary modelState)
    {
        var invalid = modelState.Where(entry => entry.Value is { Errors.Count: > 0 }).ToList();
        var bodyErrors = invalid.Where(entry => entry.Key == "" || entry.Key.StartsWith('$')).ToList();

        if (bodyErrors.Count == 0)
        {
            return invalid.ToDictionary(
                entry => ToJsonFieldName(entry.Key),
                entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());
        }

        var unknownFields = bodyErrors
            .SelectMany(entry => entry.Value!.Errors)
            .Select(error => UnmappedMemberPattern().Match(error.ErrorMessage))
            .Where(match => match.Success)
            .Select(match => match.Groups["name"].Value)
            .Distinct()
            .ToList();

        if (unknownFields.Count > 0)
        {
            return unknownFields.ToDictionary(name => name, _ => new[] { "This field is not allowed." });
        }

        // Well-formed JSON with a value of the wrong type, such as "categoryId": "three".
        var wrongTypeFields = bodyErrors
            .Where(entry => entry.Key.StartsWith("$.")
                && entry.Value!.Errors.Any(error => error.ErrorMessage.StartsWith("The JSON value could not be converted")))
            .Select(entry => entry.Key[2..])
            .Distinct()
            .ToList();

        if (wrongTypeFields.Count > 0)
        {
            return wrongTypeFields.ToDictionary(name => name, _ => new[] { "The value has the wrong type." });
        }

        return new Dictionary<string, string[]> { ["body"] = ["The request body must be valid JSON."] };
    }

    // Model state uses C# names such as "Email"; the contract uses JSON names such as "email".
    private static string ToJsonFieldName(string key) =>
        string.Join('.', key.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));

    [GeneratedRegex("^The JSON property '(?<name>[^']+)' could not be mapped")]
    private static partial Regex UnmappedMemberPattern();
}
