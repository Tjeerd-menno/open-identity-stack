import {
  createRole,
  deleteRole,
  getPlatformPermissionCatalog,
  getRole,
  getRoles,
  updateRole,
} from './roles-api';

const apiBase = 'http://localhost:5000';

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

describe('roles-api', () => {
  it('uses the admin roles list endpoint with page, pageSize, and search', async () => {
    const fetchMock = vi.fn(() => jsonResponse({
      items: [],
      totalCount: 0,
      page: 2,
      pageSize: 25,
      totalPages: 0,
    }));
    vi.stubGlobal('fetch', fetchMock);

    await getRoles({ page: 2, pageSize: 25, search: 'admin' });

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/roles?page=2&pageSize=25&search=admin`,
      expect.anything()
    );
  });

  it('maps role CRUD calls to the backend contract', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/roles/role-1` && method === 'GET') {
        return jsonResponse({ id: 'role-1', name: 'support', displayName: 'Support' });
      }

      if (url === `${apiBase}/api/admin/roles` && method === 'POST') {
        return jsonResponse({ id: 'role-2' }, 201);
      }

      if (url === `${apiBase}/api/admin/roles/role-1` && method === 'PUT') {
        return jsonResponse({ id: 'role-1' });
      }

      if (url === `${apiBase}/api/admin/roles/role-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    await getRole('role-1');
    await createRole({
      name: 'support',
      displayName: 'Support',
      description: 'Support team',
      permissions: ['users:read'],
      acknowledgeWildcardGrant: false,
    });
    await updateRole('role-1', {
      displayName: 'Support operator',
      description: 'Operators',
      permissions: ['users:read', 'sessions:read'],
      acknowledgeWildcardGrant: false,
    });
    await deleteRole('role-1');

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/roles`,
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          name: 'support',
          displayName: 'Support',
          description: 'Support team',
          permissions: ['users:read'],
          acknowledgeWildcardGrant: false,
        }),
      })
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/roles/role-1`,
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({
          displayName: 'Support operator',
          description: 'Operators',
          permissions: ['users:read', 'sessions:read'],
          acknowledgeWildcardGrant: false,
        }),
      })
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/roles/role-1`,
      expect.objectContaining({ method: 'DELETE' })
    );
  });

  it('loads the platform permission catalog from the consolidated admin permission endpoint', async () => {
    const fetchMock = vi.fn(() => jsonResponse({
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
    }));
    vi.stubGlobal('fetch', fetchMock);

    await getPlatformPermissionCatalog();

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/permissions/platform`,
      expect.anything()
    );
  });
});
