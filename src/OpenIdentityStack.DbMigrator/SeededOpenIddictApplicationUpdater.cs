using OpenIddict.Abstractions;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.DbMigrator;

public static class SeededOpenIddictApplicationUpdater
{
    private const string applicationIdSetting = "openidentitystack:application-id";

    public static void AttachProjectionIdentity(
        OpenIddictApplicationDescriptor descriptor,
        DomainApplicationId applicationId) =>
        descriptor.Settings[applicationIdSetting] = applicationId.Value.ToString();

    public static async Task UpdateAsync(
        IOpenIddictApplicationManager applicationManager,
        object existingApplication,
        OpenIddictApplicationDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        System.Collections.Immutable.ImmutableDictionary<string, string> settings =
            await applicationManager.GetSettingsAsync(existingApplication, cancellationToken);
        foreach ((string key, string value) in settings)
        {
            if (!descriptor.Settings.ContainsKey(key))
            {
                descriptor.Settings[key] = value;
            }
        }

        await applicationManager.UpdateAsync(existingApplication, descriptor, cancellationToken);
    }
}
