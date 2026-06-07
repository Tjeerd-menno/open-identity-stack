import { describe, expect, it } from 'vitest';
import {
  extractGrantedPermissions,
  hasAnyPermission,
  hasEveryPermission,
  hasPermission,
} from '@openidentitystack/permission-semantics';

const jwtWithPayload = (payload: object) => {
  const encodedPayload = btoa(JSON.stringify(payload))
    .replaceAll('+', '-')
    .replaceAll('/', '_')
    .replaceAll('=', '');
  return `header.${encodedPayload}.signature`;
};

describe('shared permission semantics', () => {
  it('extracts explicit permissions and non-standard scopes from profile and token claims', () => {
    const token = jwtWithPayload({
      permissions: ['roles:read'],
      scp: ['groups:read', 'openid'],
    });

    expect(
      extractGrantedPermissions({
        profile: {
          permission: 'users:read, users:write',
          scope: 'openid profile applications:read',
        },
        accessToken: token,
      })
    ).toEqual(['users:read', 'users:write', 'applications:read', 'roles:read', 'groups:read']);
  });

  it('does not infer wildcard permission from admin role names', () => {
    expect(
      extractGrantedPermissions({
        profile: {
          role: 'admin',
          roles: ['super-admin'],
          'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'admin',
        },
      })
    ).toEqual([]);
  });

  it('deduplicates permissions case-insensitively while preserving first observed casing', () => {
    expect(
      extractGrantedPermissions({
        profile: {
          permission: ['Users:Read', 'users:read', 'roles:read'],
        },
      })
    ).toEqual(['Users:Read', 'roles:read']);
  });

  it('matches global, resource, and nested resource wildcards', () => {
    expect(hasPermission(['*'], 'users:read')).toBe(true);
    expect(hasPermission(['*'], 'application-permissions:read')).toBe(true);
    expect(hasPermission(['*'], 'patient-api:records:read')).toBe(false);
    expect(hasPermission(['applications:*'], 'applications:manage-credentials')).toBe(true);
    expect(hasPermission(['patient-api:records:*'], 'patient-api:records:read')).toBe(true);
    expect(hasPermission(['patient-api:records:*'], 'patient-api:records:read:secret')).toBe(false);
    expect(hasPermission(['applications:*'], 'roles:read')).toBe(false);
  });

  it('evaluates any and every collections through the same matcher', () => {
    expect(hasAnyPermission(['users:*'], ['roles:read', 'users:write'])).toBe(true);
    expect(hasEveryPermission(['users:*'], ['users:read', 'users:update'])).toBe(true);
    expect(hasEveryPermission(['users:read'], ['users:read', 'users:update'])).toBe(false);
  });
});
