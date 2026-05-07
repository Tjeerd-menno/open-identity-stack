using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Command to add a certificate to a service account.
/// </summary>
/// <param name="ServiceAccountId">The service account ID.</param>
/// <param name="Thumbprint">The certificate thumbprint.</param>
/// <param name="Subject">The certificate subject.</param>
/// <param name="ExpiresAt">When the certificate expires.</param>
public sealed record AddCertificateCommand(
    ServiceAccountId ServiceAccountId,
    string Thumbprint,
    string Subject,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Response from adding a certificate.
/// </summary>
/// <param name="CertificateId">The ID of the added certificate.</param>
public sealed record AddCertificateResponse(Guid CertificateId);

/// <summary>
/// Use case interface for adding certificates to service accounts.
/// </summary>
public interface IAddCertificateUseCase
{
    /// <summary>
    /// Adds a certificate to a service account.
    /// </summary>
    Task<Result<AddCertificateResponse>> ExecuteAsync(
        AddCertificateCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case implementation for adding certificates.
/// </summary>
public sealed class AddCertificateUseCase : IAddCertificateUseCase
{
    private readonly IServiceAccountRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;

    public AddCertificateUseCase(
        IServiceAccountRepository repository,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog)
    {
        this.repository = repository;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
    }

    /// <inheritdoc />
    public async Task<Result<AddCertificateResponse>> ExecuteAsync(
        AddCertificateCommand command,
        CancellationToken cancellationToken = default)
    {
        ServiceAccount? serviceAccount = await this.repository.GetByIdAsync(command.ServiceAccountId, cancellationToken);
        if (serviceAccount is null)
        {
            return ServiceAccountErrors.NotFound;
        }

        if (serviceAccount.Status == ServiceAccountStatus.Disabled)
        {
            return ServiceAccountErrors.Disabled;
        }

        Result<Guid> addResult = serviceAccount.AddCertificate(
            command.Thumbprint,
            command.Subject,
            command.ExpiresAt,
            this.dateTimeProvider);

        if (addResult.IsFailure)
        {
            return addResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);

        await this.auditLog.LogAsync(
            userId: "system",
            action: "ServiceAccount.CertificateAdded",
            entityType: "ServiceAccount",
            entityId: serviceAccount.Id.Value.ToString(),
            details: $"CertificateId: {addResult.Value}, Thumbprint: {command.Thumbprint}, Subject: {command.Subject}",
            cancellationToken: cancellationToken);

        return new AddCertificateResponse(addResult.Value);
    }
}
