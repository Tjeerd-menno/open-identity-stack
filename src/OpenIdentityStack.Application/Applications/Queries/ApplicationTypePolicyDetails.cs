using OpenIdentityStack.Domain.Applications;

namespace OpenIdentityStack.Application.Applications.Queries;

public sealed record ApplicationTypePolicyDetails(
    ApplicationType ApplicationType,
    bool IsSelectable,
    string? UnavailabilityReason,
    ClientProfile DefaultClientProfile,
    IReadOnlyList<ClientProfile> AllowedClientProfiles,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> DefaultGrantTypes,
    IReadOnlyDictionary<string, ApplicationOptionAvailability> Options,
    bool RequirePkce,
    bool DefaultRequirePkce,
    bool DefaultRequireConsent,
    bool RequiresRedirectUris);
