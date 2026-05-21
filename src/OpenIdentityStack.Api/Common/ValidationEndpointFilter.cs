using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace OpenIdentityStack.Api.Common;

/// <summary>
/// Endpoint filter that validates request parameters using DataAnnotations.
/// </summary>
public class ValidationEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "All API request DTOs are rooted in OpenIdentityStackApiJsonContext for the Native AOT publish path.")]
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (object? argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var validationContext = new ValidationContext(argument);
            var validationResults = new List<ValidationResult>();

            if (!Validator.TryValidateObject(argument, validationContext, validationResults, validateAllProperties: true))
            {
                IDictionary<string, string[]> errors = validationResults
                    .GroupBy(r => r.MemberNames.FirstOrDefault() ?? string.Empty)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(r => r.ErrorMessage ?? "Validation error").ToArray());

                return Results.ValidationProblem(errors);
            }
        }

        return await next(context);
    }
}
