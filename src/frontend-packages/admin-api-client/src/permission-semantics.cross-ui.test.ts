import { describe, expect, it } from 'vitest';
import {
  extractGrantedPermissions,
  hasEveryPermission,
  hasPermission,
} from './permission-semantics';

function jwtWithPayload(payload: unknown): string {
  const encodedPayload = btoa(JSON.stringify(payload))
    .replaceAll('+', '-')
    .replaceAll('/', '_')
    .replaceAll('=', '');

  return `header.${encodedPayload}.signature`;
}

describe('cross-UI permission semantics regressions', () => {
  it.each([
    {
      name: 'profile permission claims',
      source: {
        profile: {
          permission: 'users:read',
          permissions: ['roles:read', 'roles:update'],
        },
      },
      expected: ['users:read', 'roles:read', 'roles:update'],
    },
    {
      name: 'access-token fallback claims',
      source: {
        profile: {},
        accessToken: jwtWithPayload({
          permissions: ['sessions:read'],
          permission: ['sessions:revoke'],
        }),
      },
      expected: ['sessions:revoke', 'sessions:read'],
    },
    {
      name: 'scope-derived permissions',
      source: {
        profile: {
          scope: 'openid profile email api inventory:read inventory:write',
          scp: 'audit-logs:read',
        },
      },
      expected: ['inventory:read', 'inventory:write', 'audit-logs:read'],
    },
    {
      name: 'role-only claims denied',
      source: {
        profile: {
          role: 'super-admin',
          roles: ['admin'],
        },
        accessToken: jwtWithPayload({
          roles: ['super-admin'],
        }),
      },
      expected: [],
    },
  ])('extracts $name consistently', ({ source, expected }) => {
    expect(extractGrantedPermissions(source)).toEqual(expected);
  });

  it.each([
    {
      name: 'global wildcard grant',
      granted: ['*'],
      required: 'application-permissions:read',
      expected: true,
    },
    {
      name: 'resource wildcard grant',
      granted: ['users:*'],
      required: 'users:update',
      expected: true,
    },
    {
      name: 'nested resource wildcard grant',
      granted: ['application:resource:*'],
      required: 'application:resource:read',
      expected: true,
    },
    {
      name: 'resource wildcard does not match deeper requirements',
      granted: ['users:*'],
      required: 'users:settings:update',
      expected: false,
    },
  ])('evaluates $name consistently', ({ granted, required, expected }) => {
    expect(hasPermission(granted, required)).toBe(expected);
  });

  it('requires every permission through wildcard-aware matching', () => {
    expect(hasEveryPermission(['application-permissions:*'], [
      'application-permissions:read',
      'application-permissions:write',
    ])).toBe(true);
  });
});
