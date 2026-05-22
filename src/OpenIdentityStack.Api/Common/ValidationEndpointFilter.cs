using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace OpenIdentityStack.Api.Common;

/// <summary>
/// Endpoint filter that validates request parameters using DataAnnotations.
/// </summary>
public class ValidationEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (object? argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var validationContext = new ValidationContext(
                argument,
                displayName: argument.GetType().Name,
                serviceProvider: null,
                items: null);
            var validationResults = new List<ValidationResult>();

            // Validator.TryValidateObject uses reflection over DataAnnotations attributes.
            // All request DTO types that reach this filter are registered in
            // OpenIdentityStackApiJsonContext, which roots them and their attribute metadata
            // so they are not removed by the trimmer.
            [UnconditionalSuppressMessage("Trimming", "IL2026",
                Justification = "All validated DTO types are registered in OpenIdentityStackApiJsonContext, preserving their DataAnnotations metadata.")]
            static bool TryValidate(object obj, ValidationContext vc, List<ValidationResult> results) =>
                Validator.TryValidateObject(obj, vc, results, validateAllProperties: true);

            if (!TryValidate(argument, validationContext, validationResults))
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
