import { screen, waitFor } from '@testing-library/react';
import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { AuthContextProvider, type AuthContextValue } from '@/lib/auth-context';
import { renderManagementWeb } from '@/test/render';
import { AppRoutes } from './AppRoutes';
import { AuthenticatedRoute, managementRoutePermissions, RequirePermission } from './route-guards';

function renderGuard(ui: ReactElement, auth: Partial<AuthContextValue> = {}, initialEntries?: string[]) {
  const value: AuthContextValue = {
    isAuthenticated: true,
    isLoading: false,
    displayName: 'Test Operator',
    permissions: ['*'],
    login: vi.fn(),
    logout: vi.fn(),
    getAccessToken: vi.fn(async () => 'token'),
    ...auth,
  };

  return {
    ...renderManagementWeb(
      <AuthContextProvider value={value}>
        {ui}
      </AuthContextProvider>,
      { initialEntries }
    ),
    auth: value,
  };
}

describe('AppRoutes route guards', () => {
  it('routes / to the Overview surface', async () => {
    renderGuard(<AppRoutes />, { permissions: ['*'] }, ['/']);

    expect(await screen.findByRole('heading', { name: 'Overview' })).toBeInTheDocument();
  });

  it('renders access denied when an authenticated operator lacks the route permission', async () => {
    renderGuard(
      <RequirePermission requiredPermissions={managementRoutePermissions.audit}>
        <h1>Audit</h1>
      </RequirePermission>,
      { permissions: ['users:read'] }
    );

    expect(screen.getByText('Access denied')).toBeInTheDocument();
    expect(screen.getByText(/audit-logs:read/i)).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Audit' })).not.toBeInTheDocument();
  });

  it('renders the route when the operator has the required permission', async () => {
    renderGuard(
      <RequirePermission requiredPermissions={managementRoutePermissions.audit}>
        <h1>Audit</h1>
      </RequirePermission>,
      { permissions: ['audit-logs:read'] }
    );

    expect(screen.getByRole('heading', { name: 'Audit' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: /access denied/i })).not.toBeInTheDocument();
  });

  it('redirects unauthenticated operators through login', async () => {
    const login = vi.fn();
    renderGuard(
      <AuthenticatedRoute>
        <h1>Applications</h1>
      </AuthenticatedRoute>,
      { isAuthenticated: false, permissions: [], login }
    );

    await waitFor(() => {
      expect(login).toHaveBeenCalledTimes(1);
    });
  });

  it('does not define legacy Clients or Service Accounts permission surfaces', () => {
    expect(Object.keys(managementRoutePermissions)).not.toContain('clients');
    expect(Object.keys(managementRoutePermissions)).not.toContain('serviceAccounts');
  });

  it('uses backend permission constants for retained route surfaces', () => {
    expect(managementRoutePermissions.settings).toEqual(['system:settings']);
    expect(managementRoutePermissions.audit).toEqual(['audit-logs:read']);
  });

  it.each(['/users', '/users/create', '/users/user-1', '/users/user-1/edit'])(
    'routes %s to the Users surface',
    async (path) => {
      vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(new Response(JSON.stringify({
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }))));

      renderGuard(<AppRoutes />, { permissions: ['*'] }, [path]);

      expect(await screen.findByRole('heading', { name: 'Users' })).toBeInTheDocument();
    }
  );

  it.each([
    ['/roles', 'Roles'],
    ['/roles/new', 'Create role'],
    ['/roles/role-1', 'Support Operator'],
  ])('routes %s to the Roles surface', async (path, heading) => {
    const apiBase = 'http://localhost:5000';
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=20` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          items: [
            {
              id: 'role-1',
              name: 'support',
              displayName: 'Support Operator',
              description: 'Support desk',
              isSystemRole: false,
              isActive: true,
              permissions: ['users:read'],
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/roles/role-1` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          id: 'role-1',
          name: 'support',
          displayName: 'Support Operator',
          description: 'Support desk',
          isSystemRole: false,
          isActive: true,
          permissions: ['users:read'],
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/permissions/platform` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          items: [
            {
              permission: 'users:read',
              resource: 'users',
              action: 'read',
              kind: 'concrete',
              displayName: 'Read Users',
              assignable: true,
            },
          ],
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }));

    renderGuard(<AppRoutes />, { permissions: ['*'] }, [path]);

    expect(await screen.findByRole('heading', { name: heading })).toBeInTheDocument();
  });

  it.each([
    ['/groups', 'Groups'],
    ['/groups/new', 'Create group'],
    ['/groups/group-1', 'Engineering'],
    ['/groups/group-1/edit', 'Edit group'],
  ])('routes %s to the Groups surface', async (path, heading) => {
    const apiBase = 'http://localhost:5000';
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/groups?page=1&pageSize=20` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          items: [
            {
              id: 'group-1',
              name: 'Engineering',
              description: 'Engineering team',
              parentGroupId: null,
              memberCount: 1,
              mappingCount: 1,
              createdAt: '2026-01-01T00:00:00Z',
              modifiedAt: null,
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/groups/group-1` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          id: 'group-1',
          name: 'Engineering',
          description: 'Engineering team',
          parentGroupId: null,
          memberCount: 1,
          mappingCount: 1,
          createdAt: '2026-01-01T00:00:00Z',
          modifiedAt: null,
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/groups/group-1/members?page=1&pageSize=20` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          items: [{ id: 'member-1', userId: 'user-1', email: 'ada@example.com', displayName: 'Ada Lovelace' }],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/groups/group-1/mappings` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          items: [{ id: 'mapping-1', type: 'Claim', value: 'department:engineering', createdAt: '2026-01-01T00:00:00Z' }],
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }));

    renderGuard(<AppRoutes />, { permissions: ['*'] }, [path]);

    expect(await screen.findByRole('heading', { name: heading })).toBeInTheDocument();
  });

  it.each([
    ['/sessions', 'Sessions'],
    ['/sessions/session-1', 'Session Details'],
  ])('routes %s to the Sessions surface', async (path, heading) => {
    const apiBase = 'http://localhost:5000';
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/sessions?page=1&pageSize=20&status=Active` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          items: [
            {
              id: 'session-1',
              userId: 'user-1',
              ipAddress: '10.0.0.5',
              userAgent: 'Firefox on Windows',
              status: 'Active',
              clientCount: 2,
              lastActivityAt: '2026-01-02T10:00:00Z',
              expiresAt: '2026-01-02T12:00:00Z',
              createdAt: '2026-01-02T08:00:00Z',
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/sessions/session-1` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          id: 'session-1',
          userId: 'user-1',
          ipAddress: '10.0.0.5',
          userAgent: 'Firefox on Windows',
          status: 'Active',
          clientCount: 2,
          lastActivityAt: '2026-01-02T10:00:00Z',
          expiresAt: '2026-01-02T12:00:00Z',
          createdAt: '2026-01-02T08:00:00Z',
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }));

    renderGuard(<AppRoutes />, { permissions: ['*'] }, [path]);

    expect(await screen.findByRole('heading', { name: heading })).toBeInTheDocument();
  });

  it.each([
    ['/providers', 'Identity Providers'],
    ['/providers/new', 'Create provider'],
    ['/providers/provider-1', 'Google Login'],
    ['/providers/provider-1/edit', 'Edit provider'],
  ])('routes %s to the Providers surface', async (path, heading) => {
    const apiBase = 'http://localhost:5000';
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/providers?includeDisabled=true` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify([
          {
            id: 'provider-1',
            name: 'google',
            displayName: 'Google Login',
            authority: 'https://accounts.google.com',
            clientId: 'google-client',
            scopes: ['openid', 'profile', 'email'],
            jitProvisioningEnabled: true,
            status: 'Active',
            createdAt: '2026-01-01T00:00:00Z',
          },
        ]), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/providers/provider-1` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          id: 'provider-1',
          name: 'google',
          displayName: 'Google Login',
          authority: 'https://accounts.google.com',
          clientId: 'google-client',
          scopes: ['openid', 'profile', 'email'],
          jitProvisioningEnabled: true,
          status: 'Active',
          createdAt: '2026-01-01T00:00:00Z',
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }));

    renderGuard(<AppRoutes />, { permissions: ['*'] }, [path]);

    expect(await screen.findByRole('heading', { name: heading })).toBeInTheDocument();
  });

  it('routes /settings to the Settings surface', async () => {
    const apiBase = 'http://localhost:5000';
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/authentication-settings` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          defaultProviderId: 'local',
          isLocalDefault: true,
          localFallbackEnabled: true,
          updatedAt: '2026-01-01T00:00:00Z',
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/authentication-settings/providers` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify([
          { id: 'local', name: 'local', displayName: 'Local Accounts', type: 'local', isActive: true },
        ]), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }));

    renderGuard(<AppRoutes />, { permissions: ['*'] }, ['/settings']);

    expect(await screen.findByRole('heading', { name: 'Authentication Settings' })).toBeInTheDocument();
  });

  it.each([
    ['/application-permissions', 'Permissions'],
    ['/application-permissions/new', 'Add Application'],
    ['/application-permissions/application-1', 'Patient API'],
  ])('routes %s to the Application Permissions surface', async (path, heading) => {
    const apiBase = 'http://localhost:5000';
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url.startsWith(`${apiBase}/api/admin/application-permissions/applications?page=`) && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          items: [
            {
              id: 'application-1',
              applicationIdentifier: 'patient-api',
              displayName: 'Patient API',
              ownerId: 'owner-1',
              ownerType: 'User',
              permissionCount: 1,
              status: 'Active',
              manifestVersion: '1.0.0',
              updatedAt: '2026-01-01T00:00:00Z',
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/application-permissions/applications/application-1` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({
          id: 'application-1',
          applicationIdentifier: 'patient-api',
          displayName: 'Patient API',
          description: 'Patient records',
          ownerId: 'owner-1',
          ownerType: 'User',
          permissionCount: 1,
          status: 'Active',
          manifestVersion: '1.0.0',
          schemaVersion: '1.0.0',
          manifestBaseUrl: null,
          concurrencyToken: 7,
          permissions: [
            {
              id: 'permission-1',
              key: 'patient:read',
              fullPermissionKey: 'patient:read',
              displayName: 'Read patients',
              description: 'Allows reading patient data',
              category: 'Patients',
              status: 'Active',
            },
          ],
          maintainers: [],
        }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/application-permissions/catalog?page=1&pageSize=50` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 50, totalPages: 0 }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/application-permissions/history?applicationIdentifier=patient-api` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({ deletedApplications: [], removedPermissions: [] }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      if (url === `${apiBase}/api/admin/application-permissions/diagnostics` && method === 'GET') {
        return Promise.resolve(new Response(JSON.stringify({ issues: [] }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }));

    renderGuard(<AppRoutes />, { permissions: ['*'] }, [path]);

    expect(await screen.findByRole('heading', { name: heading })).toBeInTheDocument();
  });
});
