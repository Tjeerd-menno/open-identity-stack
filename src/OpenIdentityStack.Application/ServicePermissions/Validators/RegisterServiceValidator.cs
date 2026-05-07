using OpenIdentityStack.Application.ServicePermissions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Application.ServicePermissions.Validators;

public static class RegisterServiceValidator
{
    public static DomainError? Validate(RegisterServiceCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ServiceIdentifier))
        {
            return DomainError.Validation("RegisterService.IdentifierRequired", "Service identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(command.OwnerId))
        {
            return DomainError.Validation("RegisterService.OwnerRequired", "Owner ID is required.");
        }

        if (!Enum.TryParse(command.OwnerType, ignoreCase: true, out OwnerType ownerType) || !Enum.IsDefined(ownerType))
        {
            return DomainError.Validation("RegisterService.OwnerTypeInvalid", "Owner type is invalid.");
        }

        if (command.Permissions.Count == 0)
        {
            return DomainError.Validation("RegisterService.PermissionsRequired", "At least one permission is required.");
        }

        if (ReservedServicePermissionNamespaces.IsReserved(command.ServiceIdentifier.Trim()))
        {
            return DomainError.Conflict("RegisterService.IdentifierReserved", "Service identifier is reserved.");
        }

        return null;
    }
}
