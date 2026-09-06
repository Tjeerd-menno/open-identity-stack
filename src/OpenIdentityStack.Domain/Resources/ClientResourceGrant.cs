using ApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;
using SharedKernel;

namespace OpenIdentityStack.Domain.Resources;

public sealed class ClientResourceGrant : AggregateRoot<Guid>
{
    private readonly List<string> delegatedPermissions = [];
    private readonly List<string> applicationPermissions = [];
    public ApplicationId ClientApplicationId { get; private set; }
    public Guid ResourceId { get; private set; }
    public long Revision { get; private set; }
    public IReadOnlyList<string> DelegatedPermissions => this.delegatedPermissions.AsReadOnly();
    public IReadOnlyList<string> ApplicationPermissions => this.applicationPermissions.AsReadOnly();

    private ClientResourceGrant() { }

    public static Result<ClientResourceGrant> Create(ApplicationId clientApplicationId, Guid resourceId,
        IReadOnlyList<string> delegatedPermissions, IReadOnlyList<string> applicationPermissions)
    {
        if (clientApplicationId == ApplicationId.Empty || resourceId == Guid.Empty)
        {
            return ResourceAccessErrors.InvalidConfiguration;
        }

        var grant = new ClientResourceGrant { Id = Guid.NewGuid(), ClientApplicationId = clientApplicationId, ResourceId = resourceId };
        Result result = grant.Configure(delegatedPermissions, applicationPermissions);
        return result.IsSuccess ? grant : result.Error;
    }

    public Result Configure(IReadOnlyList<string> delegated, IReadOnlyList<string> application)
    {
        if (delegated.Count > 500 || application.Count > 500
            || delegated.Concat(application).Any(static permission => string.IsNullOrWhiteSpace(permission) || permission.Length > 255
                || permission.Any(static character => !char.IsAsciiLetterOrDigit(character) && character is not (':' or '-' or '_' or '*' or '.'))))
        {
            return ResourceAccessErrors.InvalidConfiguration;
        }

        this.delegatedPermissions.Clear();
        this.delegatedPermissions.AddRange(delegated.Select(static value => value.ToLowerInvariant()).Distinct(StringComparer.Ordinal));
        this.applicationPermissions.Clear();
        this.applicationPermissions.AddRange(application.Select(static value => value.ToLowerInvariant()).Distinct(StringComparer.Ordinal));
        this.Revision++;
        return Result.Success();
    }
}
