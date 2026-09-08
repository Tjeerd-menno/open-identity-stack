using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Api.Authorization;

public sealed partial class AdministrativeApprovalOutcomeMiddleware(
    RequestDelegate next, ILogger<AdministrativeApprovalOutcomeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IAdministrativeApproval approval)
    {
        bool succeeded = false;
        try
        {
            await next(context);
            succeeded = context.Response.StatusCode < 400;
        }
        finally
        {
            try
            {
                await approval.RecordOutcomeAsync(succeeded, CancellationToken.None);
            }
            catch (Exception)
            {
                // Approval intent is already durable. A post-commit audit outage must not
                // misreport committed state or replace the original handler exception.
                OutcomeAuditFailed(logger, succeeded);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Administrative approval outcome audit failed. Handler succeeded: {Succeeded}. Reconcile persisted approval intents with authority state before retrying.")]
    private static partial void OutcomeAuditFailed(ILogger logger, bool succeeded);
}
