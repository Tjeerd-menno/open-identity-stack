namespace OpenIdentityStack.Domain.Applications;

public sealed record ApplicationProfilePolicy(
    ApplicationProfile ApplicationProfile,
    bool IsSelectable,
    string? UnavailabilityReason,
    ClientProfile DefaultClientProfile,
    IReadOnlySet<ClientProfile> AllowedClientProfiles,
    IReadOnlySet<string> AllowedGrantTypes,
    IReadOnlySet<string> DefaultGrantTypes,
    IReadOnlyDictionary<ApplicationOptionKey, ApplicationOptionAvailability> Options,
    bool RequirePkce,
    bool DefaultRequirePkce,
    bool DefaultRequireConsent,
    bool RequiresRedirectUris);
