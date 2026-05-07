using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ServicePermissions;

public enum OwnerType
{
    User,
    Group,
    ExternalGroup
}

public sealed record ServiceOwner(string OwnerId, OwnerType OwnerType)
{
    public static Result<ServiceOwner> Create(string? ownerId, OwnerType ownerType)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return DomainError.Validation("ServiceOwner.OwnerIdRequired", "Owner ID is required.");
        }

        return new ServiceOwner(ownerId.Trim(), ownerType);
    }
}
