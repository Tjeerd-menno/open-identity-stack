using SharedKernel;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Api.Common;

/// <summary>
/// Maps domain errors to HTTP API responses.
/// </summary>
internal static class ErrorResultMapper
{
    public static IResult ToErrorResult(DomainError error)
    {
        if (error == UpstreamIdentityErrors.QuarantineRetentionRequired)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Identity evidence must be retained",
                detail: error.Description, extensions: new Dictionary<string, object?> { ["code"] = error.Code });
        }
        if (error.Code.Contains(".CredentialCutover.", StringComparison.Ordinal))
        {
            int status = error.Code.StartsWith("Forbidden.", StringComparison.Ordinal) ? 403
                : error.Code.StartsWith("Conflict.", StringComparison.Ordinal) ? 409
                : error.Code.StartsWith("NotFound.", StringComparison.Ordinal) ? 404 : 400;
            return TypedResults.Problem(statusCode: status, title: "Cutover prerequisite not satisfied", detail: error.Description,
                extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code });
        }
        if (error.Code.StartsWith("Forbidden.AdministrativeApproval.", StringComparison.Ordinal))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "Administrative approval required", detail: error.Description,
                extensions: new Dictionary<string, object?> { ["errorCode"] = error.Code });
        }

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
