using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Federation.Commands;

/// <summary>
/// Implementation of JIT provisioning use case.
/// </summary>
public sealed class JitProvisionUserUseCase : IJitProvisionUserUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IUpstreamProviderRepository providerRepository;
    private readonly IAuditLog auditLog;
    private readonly IJitProvisioningPersistence persistence;

    public JitProvisionUserUseCase(
        IUserRepository userRepository,
        IUpstreamProviderRepository providerRepository,
        IAuditLog auditLog,
        IJitProvisioningPersistence persistence)
    {
        this.userRepository = userRepository;
        this.providerRepository = providerRepository;
        this.auditLog = auditLog;
        this.persistence = persistence;
    }

    /// <inheritdoc />
    public async Task<Result<JitProvisionUserResult>> ExecuteAsync(
        JitProvisionUserCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate subject ID
        if (string.IsNullOrWhiteSpace(command.SubjectId))
        {
            return UpstreamIdentityErrors.SubjectIdRequired;
        }

        // Find the upstream provider
        UpstreamProvider? provider = await this.providerRepository.GetByIdAsync(command.ProviderId, cancellationToken);
        if (provider is null)
        {
            return UpstreamProviderErrors.NotFound;
        }

        if (!provider.IsActive)
        {
            return UpstreamProviderErrors.ProviderDisabled;
        }

        if (string.IsNullOrWhiteSpace(command.ValidatedIssuer) || string.IsNullOrWhiteSpace(command.AuthenticationAuthority))
        {
            return await this.RejectBindingAsync(command.ProviderId, cancellationToken);
        }

        if (!string.Equals(provider.Authority, command.AuthenticationAuthority, StringComparison.Ordinal) ||
            (provider.BoundIssuer is not null && !string.Equals(provider.BoundIssuer, command.ValidatedIssuer, StringComparison.Ordinal)))
        {
            return await this.RejectBindingAsync(command.ProviderId, cancellationToken);
        }

        // Check if user already exists with this upstream identity
        User? existingUser = await this.userRepository.FindByUpstreamIdentityAsync(
            command.ProviderId,
            command.SubjectId,
            cancellationToken);

        if (existingUser is not null)
        {
            if (existingUser.Status == UserStatus.Disabled)
            {
                await this.auditLog.LogAsync("federation", "Federation.AccountAssociationDenied", "User", existingUser.Id.Value.ToString(),
                    $"Local account is disabled. Provider: {command.ProviderId.Value}.", cancellationToken);
                return UserErrors.AccountDisabled;
            }

            UpstreamIdentity? identity = existingUser.UpstreamIdentities.SingleOrDefault(i => i.Matches(command.ProviderId, command.SubjectId));
            if (identity?.Issuer is null || !string.Equals(identity.Issuer, command.ValidatedIssuer, StringComparison.Ordinal))
            {
                return await this.RejectBindingAsync(command.ProviderId, cancellationToken);
            }

            provider.BindIssuer(command.ValidatedIssuer, command.AuthenticationAuthority);
            Result committed = await this.persistence.CommitAsync(existingUser.Id, command.ProviderId, isNewUser: false, cancellationToken);
            if (committed.IsFailure) { return committed.Error; }
            // User already linked with matching issuer evidence
            return new JitProvisionUserResult(
                existingUser.Id,
                IsNewUser: false,
                existingUser.Email,
                existingUser.DisplayName);
        }

        if (!provider.JitProvisioningEnabled)
        {
            await this.auditLog.LogAsync(
                "federation", "Federation.ProvisioningDenied", "UpstreamProvider",
                provider.Id.Value.ToString(), "New account provisioning is disabled.", cancellationToken);
            return DomainError.Forbidden("Federation.AuthenticationFailed", "Unable to complete sign-in.");
        }

        // Check if a user exists with the same email
        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            User? userByEmail = await this.userRepository.GetByEmailAsync(command.Email, cancellationToken);
            if (userByEmail is not null)
            {
                await this.auditLog.LogAsync(
                    "federation", "Federation.AccountAssociationDenied", "UpstreamProvider",
                    provider.Id.Value.ToString(), "Independent account-control proof required.", cancellationToken);
                return DomainError.Forbidden("Federation.AuthenticationFailed", "Unable to complete sign-in.");
            }
        }

        // Create a new federated user
        string displayName = !string.IsNullOrWhiteSpace(command.DisplayName)
            ? command.DisplayName
            : command.Email?.Split('@')[0] ?? "User";

        Result<User> userResult = User.CreateFederated(
            command.Email ?? string.Empty,
            displayName,
            command.ProviderId,
            provider.Name,
            command.SubjectId, issuer: command.ValidatedIssuer);

        if (userResult.IsFailure)
        {
            return userResult.Error;
        }

        provider.BindIssuer(command.ValidatedIssuer, command.AuthenticationAuthority);
        await this.userRepository.AddAsync(userResult.Value, cancellationToken);
        Result created = await this.persistence.CommitAsync(userResult.Value.Id, command.ProviderId, isNewUser: true, cancellationToken);
        if (created.IsFailure) { return created.Error; }

        return new JitProvisionUserResult(
            userResult.Value.Id,
            IsNewUser: true,
            userResult.Value.Email,
            userResult.Value.DisplayName);
    }

    private async Task<DomainError> RejectBindingAsync(UpstreamProviderId providerId, CancellationToken cancellationToken)
    {
        await this.auditLog.LogAsync("federation", "Federation.IssuerBindingRejected", "UpstreamProvider", providerId.Value.ToString(), "Validated issuer or independent identity binding is missing or inconsistent.", cancellationToken);
        return UpstreamProviderErrors.IdentityBindingRejected;
    }
}
