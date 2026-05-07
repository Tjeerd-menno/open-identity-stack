using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Command to validate client certificate authentication.
/// </summary>
public sealed record ValidateCertificateCommand(
    string ClientId,
    string CertificateThumbprint);

/// <summary>
/// Result of validating certificate authentication.
/// </summary>
public sealed record ValidateCertificateResult(
    ServiceAccountId ServiceAccountId,
    string ClientId,
    string DisplayName,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedGrantTypes);

/// <summary>
/// Interface for the validate certificate use case.
/// </summary>
public interface IValidateCertificateUseCase
{
    /// <summary>
    /// Validates the client certificate.
    /// </summary>
    Task<Result<ValidateCertificateResult>> ExecuteAsync(
        ValidateCertificateCommand command,
        CancellationToken cancellationToken = default);
}
