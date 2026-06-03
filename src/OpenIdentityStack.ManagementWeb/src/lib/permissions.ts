export const allPermissions = '*';

export function hasPermission(grantedPermissions: string[], requiredPermission: string): boolean {
  const requiredParts = requiredPermission.split(':');

  return grantedPermissions.some((grantedPermission) => {
    if (grantedPermission === allPermissions) {
      return requiredParts.length === 2;
    }

    if (grantedPermission.toLowerCase() === requiredPermission.toLowerCase()) {
      return true;
    }

    if (!grantedPermission.endsWith(':*')) {
      return false;
    }

    const grantedResource = grantedPermission.slice(0, -2);
    const grantedParts = grantedResource.split(':');

    return (
      requiredParts.length === grantedParts.length + 1 &&
      grantedResource.toLowerCase() === requiredParts.slice(0, grantedParts.length).join(':').toLowerCase()
    );
  });
}

export function hasAnyPermission(grantedPermissions: string[], requiredPermissions: string[]): boolean {
  return requiredPermissions.some((requiredPermission) => hasPermission(grantedPermissions, requiredPermission));
}

export function hasEveryPermission(grantedPermissions: string[], requiredPermissions: string[]): boolean {
  return requiredPermissions.every((requiredPermission) => hasPermission(grantedPermissions, requiredPermission));
}
