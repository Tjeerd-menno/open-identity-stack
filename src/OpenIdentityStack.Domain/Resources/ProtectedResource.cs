using SharedKernel;

namespace OpenIdentityStack.Domain.Resources;

public sealed class ProtectedResource : AggregateRoot<Guid>
{
    public const string AdministrativeAudience = "urn:openidentitystack:admin-api";
    public const string AdministrativeScope = "ois.admin";
    public const string PlatformNamespace = "openidentitystack";
    public static readonly Guid AdministrativeResourceId = new("be4f5839-44e9-48fc-8866-560cc8d97686");
    private readonly List<string> permissionNamespaces = [];

    public string Audience { get; private set; } = string.Empty;
    public string Scope { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;
    public long Revision { get; private set; } = 1;
    public IReadOnlyList<string> PermissionNamespaces => this.permissionNamespaces.AsReadOnly();
    public bool IsAdministrative => this.Id == AdministrativeResourceId;

    private ProtectedResource() { }

    public static Result<ProtectedResource> Create(string audience, string scope, string displayName, IReadOnlyList<string> namespaces)
    {
        if (!Uri.TryCreate(audience, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("https" or "urn") || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0
            || audience.Length > 2048 || string.IsNullOrWhiteSpace(scope) || scope.Length > 100
            || scope.Any(static value => !char.IsAsciiLetterOrDigit(value) && value is not ('-' or '_' or '.' or ':'))
            || IsProtocolScope(scope))
        {
            return ResourceAccessErrors.InvalidConfiguration;
        }

        if (audience == AdministrativeAudience || scope == AdministrativeScope)
        {
            return ResourceAccessErrors.Reserved;
        }

        var resource = new ProtectedResource { Id = Guid.NewGuid(), Audience = audience, Scope = scope };
        Result result = resource.Configure(displayName, namespaces, enabled: true);
        return result.IsSuccess ? resource : result.Error;
    }

    public Result Configure(string displayName, IReadOnlyList<string> namespaces, bool enabled)
    {
        if (this.IsAdministrative || namespaces.Any(static value => string.Equals(value, PlatformNamespace, StringComparison.OrdinalIgnoreCase)))
        {
            return ResourceAccessErrors.Reserved;
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200 || namespaces.Count is 0 or > 50
            || namespaces.Any(static value => string.IsNullOrWhiteSpace(value) || value.Length > 63
                || value.Any(static character => !char.IsAsciiLetterOrDigit(character) && character != '-')))
        {
            return ResourceAccessErrors.InvalidConfiguration;
        }

        this.DisplayName = displayName.Trim();
        this.permissionNamespaces.Clear();
        this.permissionNamespaces.AddRange(namespaces.Select(static value => value.ToLowerInvariant()).Distinct(StringComparer.Ordinal));
        this.Enabled = enabled;
        this.Revision++;
        return Result.Success();
    }

    public static bool IsProtocolScope(string scope) => scope is "openid" or "profile" or "email" or "address" or "phone" or "offline_access" or "roles";

    public static ProtectedResource CreateAdministrative() => new()
    {
        Id = AdministrativeResourceId,
        Audience = AdministrativeAudience,
        Scope = AdministrativeScope,
        DisplayName = "OpenIdentityStack Admin API",
        permissionNamespaces = { PlatformNamespace }
    };
}

public static class ResourceAccessErrors
{
    public static readonly DomainError InvalidConfiguration = DomainError.Validation("ResourceAccess.InvalidConfiguration", "Resource access configuration is invalid.");
    public static readonly DomainError Reserved = DomainError.Forbidden("ResourceAccess.Reserved", "Administrative resources require the administrative approval workflow.");
    public static readonly DomainError UnknownResource = DomainError.Validation("ResourceAccess.UnknownResource", "The requested resource is unavailable or ambiguous.");
    public static readonly DomainError NotGranted = DomainError.Forbidden("ResourceAccess.NotGranted", "Access to the requested resource is not granted.");
}
