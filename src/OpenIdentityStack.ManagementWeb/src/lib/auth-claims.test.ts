import { describe, expect, it } from 'vitest';
import { extractGrantedPermissions } from './auth-claims';

describe('auth claim permission extraction', () => {
  it('does not infer permissions from role names alone', () => {
    expect(extractGrantedPermissions({
      profile: {
        role: 'admin',
        roles: ['super-admin'],
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'admin',
      },
    })).toEqual([]);
  });

  it('extracts permissions and scopes from the profile and access token payload', () => {
    expect(extractGrantedPermissions({
      profile: {
        permission: ['users:read'],
        scope: 'openid profile applications:read',
      },
    })).toEqual(expect.arrayContaining(['users:read', 'applications:read']));

    const payload = btoa(JSON.stringify({ permissions: ['roles:read'] }));

    expect(extractGrantedPermissions({
      profile: {},
      accessToken: `header.${payload}.signature`,
    })).toContain('roles:read');
  });

  it('reads granular permissions expanded for super admin from the access token', () => {
    const payload = btoa(JSON.stringify({
      role: ['super-admin'],
      permission: ['users:read', 'applications:read', 'system:settings'],
    }));

    expect(extractGrantedPermissions({
      profile: { role: 'super-admin' },
      accessToken: `header.${payload}.signature`,
    })).toEqual(expect.arrayContaining(['users:read', 'applications:read', 'system:settings']));
  });
});
