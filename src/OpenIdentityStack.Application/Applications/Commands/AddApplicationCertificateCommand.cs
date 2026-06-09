using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record AddApplicationCertificateCommand(
    DomainApplicationId ApplicationId,
    string Thumbprint,
    string? Subject,
    string? Description,
    DateTimeOffset? ExpiresAt);
