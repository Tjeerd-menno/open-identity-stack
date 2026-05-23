using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ApplicationPermissions;

public enum OwnerType
{
    User,
    Group,
    ExternalGroup
}

public sealed record ApplicationOwner(string OwnerId, OwnerType OwnerType)
{
    public static Result<ApplicationOwner> Create(string? ownerId, OwnerType ownerType)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return DomainError.Validation("ApplicationOwner.OwnerIdRequired", "Owner ID is required.");
        }

        return new ApplicationOwner(ownerId.Trim(), ownerType);
    }
}
