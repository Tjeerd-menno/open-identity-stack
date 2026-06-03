import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderManagementWeb } from '@/test/render';
import { GroupsPage } from './GroupsPage';

const apiBase = 'http://localhost:5000';

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

const engineeringGroup = {
  id: 'group-1',
  name: 'Engineering',
  description: 'Engineering team',
  parentGroupId: null,
  memberCount: 1,
  mappingCount: 1,
  createdAt: '2026-01-01T00:00:00Z',
  modifiedAt: null,
};

function groupsResponse() {
  return {
    items: [engineeringGroup],
    totalCount: 1,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  };
}

describe('GroupsPage', () => {
  it('lists, searches, creates, edits, and deletes groups', async () => {
    const user = userEvent.setup();
    let groupName = 'Engineering';
    let groupDescription = 'Engineering team';
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/groups?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse(groupsResponse());
      }

      if (url === `${apiBase}/api/admin/groups?page=1&pageSize=20&search=eng` && method === 'GET') {
        return jsonResponse(groupsResponse());
      }

      if (url === `${apiBase}/api/admin/groups` && method === 'POST') {
        return jsonResponse({ ...engineeringGroup, id: 'group-2', name: 'Support' }, 201);
      }

      if (url === `${apiBase}/api/admin/groups/group-1` && method === 'GET') {
        return jsonResponse({ ...engineeringGroup, name: groupName, description: groupDescription });
      }

      if (url === `${apiBase}/api/admin/groups/group-1` && method === 'PATCH') {
        const body = JSON.parse(init?.body?.toString() ?? '{}') as { name: string; description: string };
        groupName = body.name;
        groupDescription = body.description;
        return jsonResponse({ ...engineeringGroup, name: groupName, description: groupDescription });
      }

      if (url === `${apiBase}/api/admin/groups/group-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    const { unmount } = renderManagementWeb(<GroupsPage permissions={['groups:read', 'groups:write', 'groups:delete']} />);

    expect(await screen.findByRole('heading', { name: /^groups$/i })).toBeInTheDocument();
    expect(await screen.findByText('Engineering')).toBeInTheDocument();

    await user.type(screen.getByLabelText(/search groups/i), 'eng');
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/groups?page=1&pageSize=20&search=eng`,
        expect.anything()
      );
    });

    await user.click(screen.getByRole('button', { name: /new group/i }));
    await user.type(await screen.findByLabelText(/group name/i), 'Support');
    await user.type(screen.getByLabelText(/description/i), 'Support desk');
    await user.click(screen.getByRole('button', { name: /create group/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/groups`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({ name: 'Support', description: 'Support desk' }),
        })
      );
    });

    unmount();
    renderManagementWeb(
      <GroupsPage permissions={['groups:read', 'groups:write', 'groups:delete']} />,
      { initialEntries: ['/groups/group-1'] }
    );

    expect(await screen.findByRole('heading', { name: /engineering/i })).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: /group details/i })).getByText(/engineering team/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /edit group/i }));
    await user.clear(screen.getByLabelText(/group name/i));
    await user.type(screen.getByLabelText(/group name/i), 'Platform Engineering');
    await user.click(screen.getByRole('button', { name: /save group/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/groups/group-1`,
        expect.objectContaining({
          method: 'PATCH',
          body: JSON.stringify({ name: 'Platform Engineering', description: 'Engineering team' }),
        })
      );
    });

    expect(await screen.findByRole('heading', { name: /platform engineering/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^delete group$/i }));
    await user.click(await screen.findByRole('button', { name: /delete platform engineering/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/groups/group-1`,
        expect.objectContaining({ method: 'DELETE' })
      );
    });
  });

  it('manages group members and mappings with granular permissions', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/groups/group-1` && method === 'GET') {
        return jsonResponse(engineeringGroup);
      }

      if (url === `${apiBase}/api/admin/groups/group-1/members?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [{ id: 'user-1', userId: 'user-1', email: 'ada@example.com', displayName: 'Ada Lovelace' }],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/users?page=1&pageSize=100` && method === 'GET') {
        return jsonResponse({
          items: [{ id: 'user-2', email: 'grace@example.com', displayName: 'Grace Hopper', status: 'Active', createdAt: '2026-01-02T00:00:00Z' }],
          totalCount: 1,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=100` && method === 'GET') {
        return jsonResponse({
          items: [{ id: 'role-1', name: 'operator', displayName: 'Operator', description: null, isSystemRole: false, isActive: true, permissions: ['users:read'] }],
          totalCount: 1,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/groups/group-1/members/user-2` && method === 'POST') {
        return Promise.resolve(new Response(null, { status: 201 }));
      }

      if (url === `${apiBase}/api/admin/groups/group-1/members/user-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      if (url === `${apiBase}/api/admin/groups/group-1/mappings` && method === 'GET') {
        return jsonResponse({ items: [{ id: 'mapping-1', type: 'Claim', value: 'department:engineering', createdAt: '2026-01-01T00:00:00Z' }] });
      }

      if (url === `${apiBase}/api/admin/groups/group-1/mappings` && method === 'POST') {
        return jsonResponse({ id: 'mapping-2', type: 'Claim', value: 'tier:gold', createdAt: '2026-01-02T00:00:00Z' }, 201);
      }

      if (url === `${apiBase}/api/admin/groups/group-1/mappings/mapping-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(
      <GroupsPage permissions={['groups:read', 'groups:write', 'groups:manage-members']} />,
      { initialEntries: ['/groups/group-1'] }
    );

    expect(await screen.findByText(/ada lovelace/i)).toBeInTheDocument();
    expect(screen.getByText(/department:engineering/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /add member/i }));
    await user.selectOptions(await screen.findByLabelText(/select user/i), 'user-2');
    await user.click(within(screen.getByRole('dialog', { name: /add member/i })).getByRole('button', { name: /^add member$/i }));

    await user.click(screen.getByRole('button', { name: /remove member ada lovelace/i }));
    await user.click(within(await screen.findByRole('dialog', { name: /remove member/i })).getByRole('button', { name: /remove member/i }));
    await waitFor(() => {
      const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
      expect(calls).toContain(`DELETE ${apiBase}/api/admin/groups/group-1/members/user-1`);
    });
    await waitFor(() => {
      expect(screen.queryByRole('dialog', { name: /remove member/i })).not.toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /add mapping/i }));
    await user.selectOptions(await screen.findByLabelText(/mapping type/i), 'Claim');
    await user.type(screen.getByLabelText(/mapping value/i), 'tier:gold');
    await user.click(within(screen.getByRole('dialog', { name: /add mapping/i })).getByRole('button', { name: /^add mapping$/i }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog', { name: /add mapping/i })).not.toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /add mapping/i }));
    await user.selectOptions(await screen.findByLabelText(/mapping type/i), 'Role');
    await user.selectOptions(await screen.findByLabelText(/select role/i), 'role-1');
    await user.click(within(screen.getByRole('dialog', { name: /add mapping/i })).getByRole('button', { name: /^add mapping$/i }));
    await waitFor(() => {
      expect(screen.queryByRole('dialog', { name: /add mapping/i })).not.toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /remove mapping department:engineering/i }));
    await user.click(within(await screen.findByRole('dialog', { name: /remove mapping/i })).getByRole('button', { name: /remove mapping/i }));

    await waitFor(() => {
      const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
      expect(calls).toContain(`POST ${apiBase}/api/admin/groups/group-1/members/user-2`);
      expect(calls).toContain(`DELETE ${apiBase}/api/admin/groups/group-1/members/user-1`);
      expect(calls).toContain(`POST ${apiBase}/api/admin/groups/group-1/mappings`);
      expect(calls).toContain(`DELETE ${apiBase}/api/admin/groups/group-1/mappings/mapping-1`);
    });

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/groups/group-1/mappings`,
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ type: 'Role', value: 'role-1' }),
      })
    );
  });

  it('hides privileged group actions when permissions are missing', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/groups?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse(groupsResponse());
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }));

    renderManagementWeb(<GroupsPage permissions={['groups:read']} />);

    expect(await screen.findByText(/read-only access/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /new group/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete group/i })).not.toBeInTheDocument();
  });
});
