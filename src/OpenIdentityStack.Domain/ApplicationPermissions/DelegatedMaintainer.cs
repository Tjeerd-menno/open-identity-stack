using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ApplicationPermissions;

public sealed class DelegatedMaintainer : Entity<DelegatedMaintainerId>
{
    public RegisteredApplicationId RegisteredApplicationId { get; private set; }

    public string PrincipalId { get; private set; } = string.Empty;

    public OwnerType PrincipalType { get; private set; }

    public string GrantedBy { get; private set; } = string.Empty;

    public DateTimeOffset GrantedAt { get; private set; }

    private DelegatedMaintainer()
    {
    }

    public static DelegatedMaintainer Create(
        RegisteredApplicationId registeredApplicationId,
        string principalId,
        OwnerType principalType,
        string grantedBy,
        IDateTimeProvider dateTimeProvider)
    {
        return new DelegatedMaintainer
        {
            Id = DelegatedMaintainerId.Create(),
            RegisteredApplicationId = registeredApplicationId,
            PrincipalId = principalId,
            PrincipalType = principalType,
            GrantedBy = grantedBy,
            GrantedAt = dateTimeProvider.UtcNow,
            CreatedAt = dateTimeProvider.UtcNow,
        };
    }
}
