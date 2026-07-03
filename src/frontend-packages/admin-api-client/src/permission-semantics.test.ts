import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import {
  extractGrantedPermissions,
  hasAnyPermission,
  hasEveryPermission,
  hasPermission,
} from './permission-semantics';

type PermissionSemanticsCase = {
  name: string;
  granted: string;
  required: string;
  matches: boolean;
};

const permissionSemanticsCases = JSON.parse(
  readFileSync(new URL('../../../../tests/PermissionSemantics/permission-semantics-cases.json', import.meta.url), 'utf8')
) as PermissionSemanticsCase[];

function jwtWithPayload(payload: unknown): string {
  return `header.${btoa(JSON.stringify(payload))}.signature`;
}

describe('permission semantics', () => {
  it('extracts granted permissions from permission, permissions, scope, and scp claims', () => {
    expect(
      extractGrantedPermissions({
        profile: {
          permission: 'users:read, roles:read',
          permissions: ['groups:read', ['applications:read']],
          scope: 'openid profile sessions:read',
          scp: ['settings:read audit-logs:read'],
        },
        accessToken: jwtWithPayload({
          permission: ['providers:read'],
          permissions: 'application-permissions:read',
          scope: 'email groups:update',
          scp: 'sessions:revoke',
        }),
      })
    ).toEqual([
      'users:read',
      'roles:read',
      'groups:read',
      'applications:read',
      'sessions:read',
      'settings:read',
      'audit-logs:read',
      'providers:read',
      'application-permissions:read',
      'groups:update',
      'sessions:revoke',
    ]);
  });

  it('ignores standard scopes', () => {
    expect(
      extractGrantedPermissions({
        profile: {
          scope: 'openid profile email api offline_access roles users:read',
          scp: ['OPENID', 'applications:read'],
        },
      })
    ).toEqual(['users:read', 'applications:read']);
  });

  it('deduplicates permissions case-insensitively while preserving first spelling', () => {
    expect(
      extractGrantedPermissions({
        profile: {
          permission: ['Users:Read', 'users:read'],
          permissions: ['USERS:READ', 'roles:read'],
          scope: 'Roles:Read',
        },
      })
    ).toEqual(['Users:Read', 'roles:read']);
  });

  it('checks exact and wildcard permissions', () => {
    expect(hasPermission(['users:read'], 'users:read')).toBe(true);
    expect(hasPermission(['Users:Read'], 'users:read')).toBe(true);
    expect(hasPermission(['*'], 'users:read')).toBe(true);
    expect(hasPermission(['users:*'], 'users:update')).toBe(true);
    expect(hasPermission(['application:resource:*'], 'application:resource:read')).toBe(true);
    expect(hasPermission(['users:*'], 'users:settings:update')).toBe(false);
    expect(hasPermission(['application:resource:*'], 'application:other:read')).toBe(false);
  });

  it('checks any and every required permission', () => {
    expect(hasAnyPermission(['users:read'], ['roles:read', 'users:read'])).toBe(true);
    expect(hasAnyPermission(['users:read'], ['roles:read', 'groups:read'])).toBe(false);
    expect(hasEveryPermission(['users:*'], ['users:read', 'users:update'])).toBe(true);
    expect(hasEveryPermission(['users:read'], ['users:read', 'users:update'])).toBe(false);
  });

  describe.each(permissionSemanticsCases)('shared permission semantics: $name', (testCase) => {
    it('matches the shared result', () => {
      expect(hasPermission([testCase.granted], testCase.required)).toBe(testCase.matches);
    });
  });

  it('does not grant permissions from role claims or role names', () => {
    expect(
      extractGrantedPermissions({
        profile: {
          role: 'admin',
          roles: ['super-admin'],
          admin: true,
          'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'admin',
        },
        accessToken: jwtWithPayload({
          role: ['admin'],
          roles: ['super-admin'],
          admin: true,
        }),
      })
    ).toEqual([]);
  });
});

