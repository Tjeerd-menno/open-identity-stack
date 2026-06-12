import { hasPermission } from '@/lib/permissions';
import type { PlatformPermissionCatalogItem } from './roles-api';

export function getEffectivePermissionCount(
  permissions: string[],
  catalogItems: PlatformPermissionCatalogItem[] | undefined
) {
  const assignableConcretePermissions = (catalogItems ?? [])
    .filter((item) => item.assignable && item.kind === 'concrete')
    .map((item) => item.permission);

  if (assignableConcretePermissions.length === 0) {
    return permissions.length;
  }

  const effectivePermissions = new Set<string>();

  for (const permission of permissions) {
    if (!isWildcardPermission(permission)) {
      effectivePermissions.add(permission);
      continue;
    }

    for (const concretePermission of assignableConcretePermissions) {
      if (isCoveredByWildcard(concretePermission, permission)) {
        effectivePermissions.add(concretePermission);
      }
    }
  }

  return effectivePermissions.size;
}

function isCoveredByWildcard(permission: string, wildcardPermission: string) {
  return hasPermission([wildcardPermission], permission);
}

function isWildcardPermission(permission: string) {
  return permission === '*' || permission.endsWith(':*');
}
