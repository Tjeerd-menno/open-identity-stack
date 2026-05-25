using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Clients;

namespace OpenIdentityStack.Infrastructure.Persistence.Applications;

internal static class ApplicationMigrationProfileInference
{
    public static ApplicationMigrationClientProfile Infer(Client client)
    {
        var grants = client.AllowedGrantTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (client.ClientType == ClientType.Confidential &&
            grants.SetEquals(["client_credentials"]))
        {
            return new ApplicationMigrationClientProfile(ApplicationProfile.MachineToMachine, RequiresMigrationReview: false);
        }

        if (client.ClientType == ClientType.Confidential && grants.Contains("authorization_code"))
        {
            return new ApplicationMigrationClientProfile(ApplicationProfile.Web, RequiresMigrationReview: false);
        }

        if (client.ClientType == ClientType.Public && grants.Contains("authorization_code"))
        {
            return new ApplicationMigrationClientProfile(ApplicationProfile.SinglePage, RequiresMigrationReview: true);
        }

        if (grants.Contains("urn:ietf:params:oauth:grant-type:device_code"))
        {
            return new ApplicationMigrationClientProfile(ApplicationProfile.Device, RequiresMigrationReview: false);
        }

        if (grants.Contains("implicit") || grants.Contains("password"))
        {
            return new ApplicationMigrationClientProfile(ApplicationProfile.Custom, RequiresMigrationReview: true);
        }

        return new ApplicationMigrationClientProfile(ApplicationProfile.Custom, RequiresMigrationReview: true);
    }
}

internal sealed record ApplicationMigrationClientProfile(
    ApplicationProfile InferredProfile,
    bool RequiresMigrationReview);
