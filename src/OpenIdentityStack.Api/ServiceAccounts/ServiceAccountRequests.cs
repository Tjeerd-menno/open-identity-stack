using OpenIdentityStack.Domain.ServiceAccounts;

namespace OpenIdentityStack.Api.ServiceAccounts;

/// <summary>
/// Request to create a new service account.
/// </summary>
public sealed record CreateServiceAccountRequest(
    string ClientId,
    string DisplayName,
    List<string> AllowedScopes,
    List<string> AllowedGrantTypes);

/// <summary>
/// Request to update a service account.
/// </summary>
public sealed record UpdateServiceAccountRequest(
    string? DisplayName,
    List<string>? AllowedScopes);

/// <summary>
/// Request to rotate a service account secret.
/// </summary>
public sealed record RotateSecretRequest(
    bool RevokeExisting = false,
    string? Description = null,
    DateTimeOffset? ExpiresAt = null);

/// <summary>
/// Request to add a certificate to a service account.
/// </summary>
public sealed record AddCertificateRequest(
    string Thumbprint,
    string Subject,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Response for a created service account.
/// </summary>
public sealed record ServiceAccountCreatedResponse(
    Guid Id,
    string ClientId,
    string DisplayName,
    string InitialSecret,
    DateTimeOffset CreatedAt);

/// <summary>
/// Response for a service account.
/// </summary>
public sealed record ServiceAccountResponse(
    Guid Id,
    string ClientId,
    string DisplayName,
    ServiceAccountStatus Status,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedGrantTypes,
    int CredentialCount,
    int CertificateCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// Response for a rotated secret.
/// </summary>
public sealed record RotateSecretResponse(
    Guid CredentialId,
    string NewSecret);

/// <summary>
/// Response for an added certificate.
/// </summary>
public sealed record AddCertificateResponse(Guid CertificateId);

/// <summary>
/// Response for listing service accounts.
/// </summary>
public sealed record ListServiceAccountsResponse(
    IReadOnlyList<ServiceAccountResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);
