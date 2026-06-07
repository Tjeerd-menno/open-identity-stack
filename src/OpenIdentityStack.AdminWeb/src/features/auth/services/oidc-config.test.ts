import { beforeEach, describe, expect, it, vi } from 'vitest';

const importOidcConfig = async () => {
  vi.resetModules();
  window.__OPENID_MODULE_CONFIG__ = {
    VITE_OIDC_AUTHORITY: 'https://identity.example.com',
    VITE_OIDC_CLIENT_ID: 'admin-web',
  };
  return await import('./oidc-config');
};

const jwtWithPayload = (payload: object) => {
  const encodedPayload = btoa(JSON.stringify(payload))
    .replaceAll('+', '-')
    .replaceAll('/', '_')
    .replaceAll('=', '');
  return `header.${encodedPayload}.signature`;
};

describe('oidc config helpers', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    delete window.__OPENID_MODULE_CONFIG__;
  });

  it('builds OIDC settings from runtime config and browser origin', async () => {
    const { oidcConfig } = await importOidcConfig();

    expect(oidcConfig.authority).toBe('https://identity.example.com');
    expect(oidcConfig.client_id).toBe('admin-web');
    expect(oidcConfig.redirect_uri).toBe(`${window.location.origin}/auth/callback`);
    expect(oidcConfig.metadata?.token_endpoint).toBe('https://identity.example.com/connect/token');
  });

  it('extracts permissions from explicit permission claims', async () => {
    const { extractPermissions } = await importOidcConfig();

    expect(extractPermissions({ profile: { permission: 'users:read' } })).toEqual(['users:read']);
    expect(extractPermissions({ profile: { permissions: ['roles:read', 'roles:create'] } })).toEqual([
      'roles:read',
      'roles:create',
    ]);
  });

  it('extracts non-standard scope permissions and ignores standard scopes', async () => {
    const { extractPermissions } = await importOidcConfig();

    expect(
      extractPermissions({
        profile: { scope: 'openid profile email inventory:read inventory:write' },
      })
    ).toEqual(['inventory:read', 'inventory:write']);
  });

  it('does not infer wildcard permission from admin role names', async () => {
    const { extractPermissions } = await importOidcConfig();

    expect(extractPermissions({ profile: { roles: ['user', 'admin'] } })).toEqual([]);
    expect(extractPermissions({ profile: { role: 'super-admin' } })).toEqual([]);
  });

  it('extracts and combines permissions from profile and access token claims', async () => {
    const { extractPermissions } = await importOidcConfig();
    const accessToken = jwtWithPayload({
      permissions: ['sessions:read'],
      scp: ['groups:read', 'openid'],
    });

    expect(
      extractPermissions({
        profile: {
          permission: ['users:read'],
          scope: 'openid profile applications:read',
        },
        access_token: accessToken,
      })
    ).toEqual(['users:read', 'applications:read', 'sessions:read', 'groups:read']);
  });

  it('falls back to access token claims when profile has no permissions', async () => {
    const { extractPermissions } = await importOidcConfig();
    const accessToken = jwtWithPayload({ permissions: ['sessions:read'] });

    expect(extractPermissions({ profile: {}, access_token: accessToken })).toEqual(['sessions:read']);
  });

  it('returns empty permissions for missing users or malformed tokens', async () => {
    const { extractPermissions } = await importOidcConfig();

    expect(extractPermissions(null)).toEqual([]);
    expect(extractPermissions({ profile: {}, access_token: 'not-a-jwt' })).toEqual([]);
  });

  it('extracts display names by preferred claim order', async () => {
    const { extractDisplayName } = await importOidcConfig();

    expect(extractDisplayName({ profile: { name: 'Alice A.' } })).toBe('Alice A.');
    expect(extractDisplayName({ profile: { display_name: 'Display Alice' } })).toBe('Display Alice');
    expect(extractDisplayName({ profile: { preferred_username: 'alice' } })).toBe('alice');
    expect(extractDisplayName({ profile: { email: 'alice@example.com' } })).toBe('alice@example.com');
    expect(extractDisplayName(null)).toBe('Unknown User');
  });
});
