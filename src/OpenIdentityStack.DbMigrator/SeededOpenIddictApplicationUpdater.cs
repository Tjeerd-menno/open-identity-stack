using OpenIddict.Abstractions;

namespace OpenIdentityStack.DbMigrator;

public static class SeededOpenIddictApplicationUpdater
{
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
