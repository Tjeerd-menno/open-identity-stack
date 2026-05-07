using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ServicePermissions;

public sealed class DelegatedMaintainer : Entity<DelegatedMaintainerId>
{
    public RegisteredServiceId RegisteredServiceId { get; private set; }

    public string PrincipalId { get; private set; } = string.Empty;

    public OwnerType PrincipalType { get; private set; }

    public string GrantedBy { get; private set; } = string.Empty;

    public DateTimeOffset GrantedAt { get; private set; }

    private DelegatedMaintainer()
    {
    }

    public static DelegatedMaintainer Create(
        RegisteredServiceId registeredServiceId,
        string principalId,
        OwnerType principalType,
        string grantedBy,
        IDateTimeProvider dateTimeProvider)
    {
        return new DelegatedMaintainer
        {
            Id = DelegatedMaintainerId.Create(),
            RegisteredServiceId = registeredServiceId,
            PrincipalId = principalId,
            PrincipalType = principalType,
            GrantedBy = grantedBy,
            GrantedAt = dateTimeProvider.UtcNow,
            CreatedAt = dateTimeProvider.UtcNow,
        };
    }
}
