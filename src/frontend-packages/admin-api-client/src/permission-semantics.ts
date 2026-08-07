export const allPermissions = '*';

export function matchesPermission(grantedPermission: string, requiredPermission: string): boolean {
  const normalizedGranted = grantedPermission.trim();
  const normalizedRequired = requiredPermission.trim();
  if (!normalizedGranted || !normalizedRequired) {
    return false;
  }

  const requiredParts = normalizedRequired.split(':');

  if (normalizedGranted === allPermissions) {
    return requiredParts.length === 2;
  }

  if (normalizedGranted.toLowerCase() === normalizedRequired.toLowerCase()) {
    return true;
  }

  if (!isTerminalWildcard(normalizedGranted)) {
    return false;
  }

  const grantedResource = normalizedGranted.slice(0, -2);
  const grantedParts = grantedResource.split(':');

  return (
    requiredParts.length === grantedParts.length + 1 &&
    grantedResource.toLowerCase() === requiredParts.slice(0, grantedParts.length).join(':').toLowerCase()
  );
}

export function hasPermission(grantedPermissions: string[], requiredPermission: string): boolean {
  return grantedPermissions.some((grantedPermission) => matchesPermission(grantedPermission, requiredPermission));
}

export function hasAnyPermission(grantedPermissions: string[], requiredPermissions: string[]): boolean {
  return requiredPermissions.some((requiredPermission) => hasPermission(grantedPermissions, requiredPermission));
}

export function hasEveryPermission(grantedPermissions: string[], requiredPermissions: string[]): boolean {
  return requiredPermissions.every((requiredPermission) => hasPermission(grantedPermissions, requiredPermission));
}

function isTerminalWildcard(permission: string): boolean {
  return permission.endsWith(':*') && permission.indexOf('*') === permission.length - 1;
}
