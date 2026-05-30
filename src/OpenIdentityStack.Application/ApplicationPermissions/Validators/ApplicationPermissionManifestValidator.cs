using System.Text.RegularExpressions;
using OpenIdentityStack.Domain.ApplicationPermissions;
using SharedKernel;

namespace OpenIdentityStack.Application.ApplicationPermissions.Validators;

public sealed record PermissionManifestDocument(
    string SchemaVersion,
    PermissionManifestApplicationDeclaration Application,
    IReadOnlyList<PermissionManifestPermissionDeclaration> Permissions);

public sealed record PermissionManifestApplicationDeclaration(
    string Id,
    string DisplayName,
    string? Description,
    string Version);

public sealed record PermissionManifestPermissionDeclaration(
    string Key,
    string DisplayName,
    string? Description,
    string? Category);

public static partial class ApplicationPermissionManifestValidator
{
    public static readonly DomainError SchemaVersionUnsupported = DomainError.Validation(
        "PermissionManifest.SchemaVersionUnsupported",
        "Manifest schemaVersion must be 1.0.0.");

    public static readonly DomainError ApplicationVersionInvalid = DomainError.Validation(
        "PermissionManifest.ApplicationVersionInvalid",
        "Manifest application.version must be valid SemVer 2.0.0 without build metadata.");

    public static readonly DomainError ApplicationIdInvalid = DomainError.Validation(
        "PermissionManifest.ApplicationIdInvalid",
        "Manifest application.id must match ^[a-z][a-z0-9-]{2,62}$.");

    public static readonly DomainError PermissionKeyInvalid = DomainError.Validation(
        "PermissionManifest.PermissionKeyInvalid",
        "Manifest permission keys must match resourceOrAggregate:action.");

    public static readonly DomainError DuplicatePermissionKeys = DomainError.Validation(
        "PermissionManifest.DuplicatePermissionKeys",
        "Manifest permission keys must be unique.");

    public static readonly DomainError ManifestBaseUrlInvalid = DomainError.Validation(
        "PermissionManifest.ManifestBaseUrlInvalid",
        "Manifest base URL must be HTTP(S), absolute, and must not contain a query or fragment.");

    public static Result Validate(PermissionManifestDocument manifest, string? manifestBaseUrl, bool allowEmptyPermissions = false)
    {
        if (!string.Equals(manifest.SchemaVersion, "1.0.0", StringComparison.Ordinal))
        {
            return SchemaVersionUnsupported;
        }

        if (!ApplicationIdRegex().IsMatch(manifest.Application.Id))
        {
            return ApplicationIdInvalid;
        }

        if (!SemVerWithoutBuildMetadataRegex().IsMatch(manifest.Application.Version))
        {
            return ApplicationVersionInvalid;
        }

        if (!allowEmptyPermissions && manifest.Permissions.Count == 0)
        {
            return RegisteredApplication.AtLeastOnePermissionRequired;
        }

        var permissionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (PermissionManifestPermissionDeclaration permission in manifest.Permissions)
        {
            if (!PermissionKeyRegex().IsMatch(permission.Key))
            {
                return PermissionKeyInvalid;
            }

            if (!permissionKeys.Add(permission.Key))
            {
                return DuplicatePermissionKeys;
            }
        }

        if (!IsValidManifestBaseUrl(manifestBaseUrl))
        {
            return ManifestBaseUrlInvalid;
        }

        return Result.Success();
    }

    private static bool IsValidManifestBaseUrl(string? manifestBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(manifestBaseUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(manifestBaseUrl, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        return string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]{2,62}$")]
    private static partial Regex ApplicationIdRegex();

    [GeneratedRegex(@"^[a-z][a-z0-9-]{1,62}:[a-z][a-z0-9-]{1,62}$")]
    private static partial Regex PermissionKeyRegex();

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:0|[1-9A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9A-Za-z-][0-9A-Za-z-]*))*)?$")]
    private static partial Regex SemVerWithoutBuildMetadataRegex();
}
