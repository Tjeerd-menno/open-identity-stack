using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Domain.Applications;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record ApplicationCommandResult(
    DomainApplicationId Id,
    string ClientId,
    string DisplayName,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    ApplicationStatus Status,
    ApplicationDetails Details);

public sealed record ApplicationCreateCommandResult(
    ApplicationCommandResult Application,
    string? InitialSecret);

public sealed record ApplicationCredentialCommandResult(
    Guid CredentialId,
    DomainApplicationId ApplicationId,
    ApplicationCredentialType Type,
    string? OneTimeSecret);

public sealed record ValidateApplicationCredentialsResult(
    DomainApplicationId ApplicationId,
    string ClientId,
    string DisplayName,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedGrantTypes);
