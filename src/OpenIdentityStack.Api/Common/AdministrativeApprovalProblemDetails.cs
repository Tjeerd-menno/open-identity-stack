using Microsoft.AspNetCore.Mvc;

namespace OpenIdentityStack.Api.Common;

/// <summary>
/// Describes either an ordinary forbidden response or an administrative approval requirement.
/// </summary>
public sealed class AdministrativeApprovalProblemDetails : ProblemDetails
{
    /// <summary>
    /// Identifies the approval action required. Absent for ordinary permission denials.
    /// </summary>
    public string? ErrorCode { get; init; }
}
