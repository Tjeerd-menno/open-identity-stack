using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace OpenIdentityStack.Api.Configuration;

/// <summary>
/// Configures RFC 7807 ProblemDetails responses, mapping common exceptions to
/// status codes/titles and enriching responses with trace and type metadata.
/// </summary>
public static class ProblemDetailsConfiguration
{
    public static IServiceCollection AddConfiguredProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                HttpContext httpContext = context.HttpContext;
                ProblemDetails problemDetails = context.ProblemDetails;

                // Get the exception if this is from exception handler middleware
                Exception? exception = context.Exception;

                // Special handling for cancellation exceptions - these are normal for OIDC flows
                // where the client closes the connection after receiving the token
                // Skip processing if response has already started or request was aborted
                if (exception is TaskCanceledException or OperationCanceledException)
                {
                    if (httpContext.Response.HasStarted || httpContext.RequestAborted.IsCancellationRequested)
                    {
                        return;
                    }
                }

                // Map exceptions to status codes and titles
                if (exception is not null && exception is not TaskCanceledException and not OperationCanceledException)
                {
                    (int statusCode, string title) = exception switch
                    {
                        Microsoft.AspNetCore.Http.BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad Request"),
                        ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
                        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
                        KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
                        InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
                        Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict"),
                        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
                    };

                    problemDetails.Status = statusCode;
                    problemDetails.Title = title;
                    httpContext.Response.StatusCode = statusCode;
                }

                problemDetails.Instance = httpContext.Request.Path;
                problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

                if (exception is not null && exception.Data.Contains("ErrorCode"))
                {
                    problemDetails.Extensions["errorCode"] = exception.Data["ErrorCode"];
                }

                if (exception is not null)
                {
                    IHostEnvironment environment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
                    if (environment.IsDevelopment())
                    {
                        problemDetails.Detail = exception.Message;
                    }
                    else
                    {
                        problemDetails.Detail = exception switch
                        {
                            Microsoft.AspNetCore.Http.BadHttpRequestException => "The request body is invalid or malformed.",
                            ArgumentException => exception.Message,
                            UnauthorizedAccessException => "You are not authorized to perform this action.",
                            KeyNotFoundException => "The requested resource was not found.",
                            InvalidOperationException => exception.Message,
                            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => "The state changed while this request was being processed. Reload and retry the operation.",
                            _ => "An unexpected error occurred. Please try again later."
                        };
                    }
                }

                // Set RFC 7231/7235 Type URIs for backward compatibility
                int currentStatusCode = problemDetails.Status ?? httpContext.Response.StatusCode;
                problemDetails.Type = currentStatusCode switch
                {
                    StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    StatusCodes.Status401Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
                    StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                    StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                    _ => problemDetails.Type ?? "https://tools.ietf.org/html/rfc7231#section-6.6.1" // Default to 500 error
                };
            };
        });

        return services;
    }
}
