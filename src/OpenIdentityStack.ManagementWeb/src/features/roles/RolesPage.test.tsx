import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderManagementWeb } from '@/test/render';
import { RolesPage } from './RolesPage';

const apiBase = 'http://localhost:5000';

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

const platformCatalog = {
  items: [
    {
      permission: 'users:read',
      resource: 'users',
      action: 'read',
      kind: 'concrete',
      displayName: 'Read Users',
      assignable: true,
    },
    {
      permission: 'roles:assign',
      resource: 'roles',
      action: 'assign',
      kind: 'concrete',
      displayName: 'Assign Roles',
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
  ],
};

function roleListResponse() {
  return {
    items: [
      {
        id: 'role-system',
        name: 'admin',
        displayName: 'Administrator',
        description: 'System administrator',
        isSystemRole: true,
        isActive: true,
        permissions: ['*'],
      },
      {
        id: 'role-custom',
        name: 'support',
        displayName: 'Support Operator',
        description: 'Support desk',
        isSystemRole: false,
        isActive: true,
        permissions: ['users:read'],
      },
    ],
    totalCount: 2,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  };
}

describe('RolesPage', () => {
  it('lists roles, searches, pages, and opens role details', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse({
          ...roleListResponse(),
          totalCount: 40,
          totalPages: 2,
        });
      }

      if (url === `${apiBase}/api/admin/roles?page=2&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [
            {
              id: 'role-audit',
              name: 'auditor',
              displayName: 'Auditor',
              description: null,
              isSystemRole: false,
              isActive: true,
              permissions: ['audit-logs:read'],
            },
          ],
          totalCount: 40,
          page: 2,
          pageSize: 20,
          totalPages: 2,
        });
      }

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=20&search=support` && method === 'GET') {
        return jsonResponse({
          items: [roleListResponse().items[1]],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/roles/role-custom` && method === 'GET') {
        return jsonResponse(roleListResponse().items[1]);
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<RolesPage permissions={['roles:read', 'roles:write']} />);

    expect(await screen.findByRole('heading', { name: /^roles$/i })).toBeInTheDocument();
    expect(await screen.findByText(/administrator/i)).toBeInTheDocument();
    expect(screen.getByText(/support operator/i)).toBeInTheDocument();
    expect(screen.getByText(/system/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /next/i }));
    expect(await screen.findByText(/^Auditor$/)).toBeInTheDocument();

    await user.clear(screen.getByLabelText(/search roles/i));
    await user.type(screen.getByLabelText(/search roles/i), 'support');
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/roles?page=1&pageSize=20&search=support`,
        expect.anything()
      );
    });

    await user.click(await screen.findByRole('button', { name: /view support operator/i }));
    expect(await screen.findByRole('heading', { name: /support operator/i })).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: /role details/i })).getByText(/users:read/i)).toBeInTheDocument();
  });

  it('creates a role with selected permissions and requires acknowledgement for wildcard grants', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse(roleListResponse());
      }

      if (url === `${apiBase}/api/admin/permissions/platform` && method === 'GET') {
        return jsonResponse(platformCatalog);
      }

      if (url === `${apiBase}/api/admin/roles` && method === 'POST') {
        return jsonResponse({ id: 'role-new', name: 'operations' }, 201);
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<RolesPage permissions={['roles:read', 'roles:write']} />, { initialEntries: ['/roles/new'] });

    await user.click(await screen.findByLabelText(/users all/i));
    await user.type(screen.getByLabelText(/^name$/i), 'operations');
    await user.type(screen.getByLabelText(/display name/i), 'Operations');
    await user.click(screen.getByRole('button', { name: /create role/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/acknowledge wildcard/i);
    await user.click(screen.getByLabelText(/acknowledge wildcard grant/i));
    await user.click(screen.getByRole('button', { name: /create role/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/roles`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({
            name: 'operations',
            displayName: 'Operations',
            description: null,
            permissions: ['users:*'],
            acknowledgeWildcardGrant: true,
          }),
        })
      );
    });
  });

  it('edits custom role metadata and permissions', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/roles/role-custom` && method === 'GET') {
        return jsonResponse(roleListResponse().items[1]);
      }

      if (url === `${apiBase}/api/admin/permissions/platform` && method === 'GET') {
        return jsonResponse(platformCatalog);
      }

      if (url === `${apiBase}/api/admin/roles/role-custom` && method === 'PUT') {
        return jsonResponse({ ...roleListResponse().items[1], displayName: 'Support Lead' });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<RolesPage permissions={['roles:read', 'roles:write']} />, { initialEntries: ['/roles/role-custom'] });

    await user.click(await screen.findByRole('button', { name: /edit role/i }));
    expect(screen.getByLabelText(/^name$/i)).toBeDisabled();
    await user.clear(screen.getByLabelText(/display name/i));
    await user.type(screen.getByLabelText(/display name/i), 'Support Lead');
    await user.click(screen.getByLabelText(/assign roles/i));
    await user.click(screen.getByRole('button', { name: /save role/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/roles/role-custom`,
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({
            displayName: 'Support Lead',
            description: 'Support desk',
            permissions: ['users:read', 'roles:assign'],
            acknowledgeWildcardGrant: false,
          }),
        })
      );
    });
  });

  it('prevents deleting system roles and deletes custom roles after confirmation', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/roles/role-system` && method === 'GET') {
        return jsonResponse(roleListResponse().items[0]);
      }

      if (url === `${apiBase}/api/admin/roles/role-custom` && method === 'GET') {
        return jsonResponse(roleListResponse().items[1]);
      }

      if (url === `${apiBase}/api/admin/roles/role-custom` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    const { unmount } = renderManagementWeb(
      <RolesPage permissions={['roles:read', 'roles:write']} />,
      { initialEntries: ['/roles/role-system'] }
    );

    expect(await screen.findByText(/system role and cannot be deleted/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^delete role$/i })).not.toBeInTheDocument();

    unmount();
    renderManagementWeb(
      <RolesPage permissions={['roles:read', 'roles:write']} />,
      { initialEntries: ['/roles/role-custom'] }
    );

    await user.click(await screen.findByRole('button', { name: /^delete role$/i }));
    await user.click(await screen.findByRole('button', { name: /delete support operator/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/roles/role-custom`,
        expect.objectContaining({ method: 'DELETE' })
      );
    });
  });

  it('renders read-only access without privileged role actions', async () => {
    vi.stubGlobal('fetch', vi.fn(() => jsonResponse(roleListResponse())));

    renderManagementWeb(<RolesPage permissions={['roles:read']} />);

    expect(await screen.findByText(/read-only access/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /new role/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^delete role$/i })).not.toBeInTheDocument();
  });
});
