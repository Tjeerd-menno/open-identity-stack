import { describe, expect, it } from 'vitest';
import { getEffectivePermissionCount } from './permission-count';
import type { PlatformPermissionCatalogItem } from './roles-api';

const catalogItems: PlatformPermissionCatalogItem[] = [
  {
    permission: 'users:read',
    resource: 'users',
    action: 'read',
    kind: 'concrete',
    displayName: 'Read Users',
    assignable: true,
  },
  {
    permission: 'users:settings:update',
    resource: 'users:settings',
    action: 'update',
    kind: 'concrete',
    displayName: 'Update User Settings',
    assignable: true,
  },
  {
    permission: 'users:*',
    resource: 'users',
    action: '*',
    kind: 'wildcard',
    displayName: 'Users All',
    assignable: true,
  },
];

describe('getEffectivePermissionCount', () => {
  it('counts only the next segment covered by a wildcard grant', () => {
    expect(getEffectivePermissionCount(['users:*'], catalogItems)).toBe(1);
  });
});
