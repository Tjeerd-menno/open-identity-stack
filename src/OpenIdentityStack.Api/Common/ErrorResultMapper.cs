using SharedKernel;

namespace OpenIdentityStack.Api.Common;

/// <summary>
/// Maps domain errors to HTTP API responses.
/// </summary>
internal static class ErrorResultMapper
{
    public static IResult ToErrorResult(DomainError error)
    {
        if (error.Code.StartsWith("NotFound.", StringComparison.Ordinal) ||
            error.Code.Equals("User.NotFound", StringComparison.Ordinal))
        {
            return TypedResults.NotFound(new { error = error.Code, message = error.Description });
        }

        if (error.Code.StartsWith("Conflict.", StringComparison.Ordinal) ||
            error.Code.Equals("User.EmailAlreadyExists", StringComparison.Ordinal))
        {
            return TypedResults.Conflict(new { error = error.Code, message = error.Description });
        }

        if (error.Code.StartsWith("Unauthorized.", StringComparison.Ordinal))
        {
            return TypedResults.Unauthorized();
        }

        if (error.Code.StartsWith("Forbidden.", StringComparison.Ordinal))
        {
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
        }

        return TypedResults.BadRequest(new { error = error.Code, message = error.Description });
    }
}
