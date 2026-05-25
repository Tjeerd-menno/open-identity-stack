using OpenIdentityStack.Domain.Applications;

namespace OpenIdentityStack.Application.Applications.Queries;

public sealed record ApplicationProfilePolicyDetails(
    ApplicationProfile ApplicationProfile,
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
