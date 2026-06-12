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

  it('counts concrete permissions covered by wildcard grants on the role list and detail views', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [
            {
              id: 'role-applications',
              name: 'application-admin',
              displayName: 'Application Administrator',
              description: 'Application operator',
              isSystemRole: true,
              isActive: true,
              permissions: ['applications:*'],
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/permissions/platform` && method === 'GET') {
        return jsonResponse({
          items: [
            ...platformCatalog.items,
            {
              permission: 'applications:*',
              resource: 'applications',
              action: '*',
              kind: 'wildcard',
              displayName: 'Applications All',
              assignable: true,
            },
            {
              permission: 'applications:read',
              resource: 'applications',
              action: 'read',
              kind: 'concrete',
              displayName: 'Read Applications',
              assignable: true,
            },
            {
              permission: 'applications:write',
              resource: 'applications',
              action: 'write',
              kind: 'concrete',
              displayName: 'Write Applications',
              assignable: true,
            },
          ],
        });
      }

      if (url === `${apiBase}/api/admin/roles/role-applications` && method === 'GET') {
        return jsonResponse({
          id: 'role-applications',
          name: 'application-admin',
          displayName: 'Application Administrator',
          description: 'Application operator',
          isSystemRole: true,
          isActive: true,
          permissions: ['applications:*'],
        });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<RolesPage permissions={['roles:read', 'roles:write']} />);

    const row = await screen.findByRole('row', { name: /application administrator/i });
    expect(within(row).getByText('2')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /view application administrator/i }));

    expect(await screen.findByText(/permission count: 2/i)).toBeInTheDocument();
  });

  it('creates a role with wildcard permissions after confirming the save dialog', async () => {
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

    const usersWildcard = await screen.findByLabelText(/users all/i);
    const readUsers = screen.getByLabelText(/read users/i);

    expect(readUsers).not.toBeChecked();
    await user.click(usersWildcard);
    expect(readUsers).toBeChecked();
    expect(screen.getByText(/permission count: 1/i)).toBeInTheDocument();
    await user.click(usersWildcard);
    expect(readUsers).not.toBeChecked();
    await user.click(usersWildcard);

    await user.type(screen.getByLabelText(/^name$/i), 'operations');
    await user.type(screen.getByLabelText(/display name/i), 'Operations');
    await user.click(screen.getByRole('button', { name: /create role/i }));

    expect(screen.queryByLabelText(/acknowledge wildcard grant/i)).not.toBeInTheDocument();
    expect(await screen.findByText(/permissions include wildcard permissions/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /save role/i }));

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

  it('does not visualize nested permissions as covered by a resource wildcard', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse(roleListResponse());
      }

      if (url === `${apiBase}/api/admin/permissions/platform` && method === 'GET') {
        return jsonResponse({
          items: [
            ...platformCatalog.items,
            {
              permission: 'users:settings:update',
              resource: 'users:settings',
              action: 'update',
              kind: 'concrete',
              displayName: 'Update User Settings',
              assignable: true,
            },
          ],
        });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<RolesPage permissions={['roles:read', 'roles:write']} />, { initialEntries: ['/roles/new'] });

    await user.click(await screen.findByLabelText(/users all/i));

    expect(screen.getByLabelText(/read users/i)).toBeChecked();
    expect(screen.getByLabelText(/update user settings/i)).not.toBeChecked();
    expect(screen.getByText(/permission count: 1/i)).toBeInTheDocument();
  });

  it('validates role create input before submitting', async () => {
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

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<RolesPage permissions={['roles:read', 'roles:write']} />, { initialEntries: ['/roles/new'] });

    expect(await screen.findByLabelText(/^name$/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /create role/i }));

    expect(await screen.findByText(/role name must be 3-50 lowercase letters/i)).toBeInTheDocument();
    expect(screen.getByText(/display name is required/i)).toBeInTheDocument();
    expect(screen.getByText(/select at least one permission/i)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalledWith(
      `${apiBase}/api/admin/roles`,
      expect.objectContaining({ method: 'POST' })
    );
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
    expect(screen.queryByRole('button', { name: /select resource/i })).not.toBeInTheDocument();
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
