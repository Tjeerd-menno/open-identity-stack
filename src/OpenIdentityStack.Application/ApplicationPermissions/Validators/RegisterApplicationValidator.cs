using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Application.ApplicationPermissions.Validators;

public static class RegisterApplicationValidator
{
    public static DomainError? Validate(RegisterApplicationCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ApplicationIdentifier))
        {
            return DomainError.Validation("RegisterApplication.IdentifierRequired", "Service identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(command.OwnerId))
        {
            return DomainError.Validation("RegisterApplication.OwnerRequired", "Owner ID is required.");
        }

        if (!Enum.TryParse(command.OwnerType, ignoreCase: true, out OwnerType ownerType) || !Enum.IsDefined(ownerType))
        {
            return DomainError.Validation("RegisterApplication.OwnerTypeInvalid", "Owner type is invalid.");
        }

        if (command.Permissions.Count == 0)
        {
            return DomainError.Validation("RegisterApplication.PermissionsRequired", "At least one permission is required.");
        }

        if (ReservedApplicationPermissionNamespaces.IsReserved(command.ApplicationIdentifier.Trim()))
        {
            return DomainError.Conflict("RegisterApplication.IdentifierReserved", "Service identifier is reserved.");
        }

        return null;
    }
}
