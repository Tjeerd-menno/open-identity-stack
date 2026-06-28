import {
  addGroupMapping,
  addMemberToGroup,
  createGroup,
  deleteGroup,
  getGroup,
  getGroupMappings,
  getGroupMembers,
  getGroups,
  removeGroupMapping,
  removeMemberFromGroup,
  updateGroup,
} from './groups-api';

const apiBase = 'http://localhost:5000';

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

describe('groups-api', () => {
  it('uses the admin groups list endpoint with page, pageSize, and search', async () => {
    const fetchMock = vi.fn(() => jsonResponse({
      items: [],
      totalCount: 0,
      page: 2,
      pageSize: 25,
      totalPages: 0,
    }));
    vi.stubGlobal('fetch', fetchMock);

    await getGroups({ page: 2, pageSize: 25, search: 'engineering' });

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/groups?page=2&pageSize=25&search=engineering`,
      expect.anything()
    );
  });

  it('maps group CRUD calls to the backend contract', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/groups/group-1` && method === 'GET') {
        return jsonResponse({ id: 'group-1', name: 'engineering', description: 'Engineering' });
      }

      if (url === `${apiBase}/api/admin/groups` && method === 'POST') {
        return jsonResponse({ id: 'group-2', name: 'support' }, 201);
      }

      if (url === `${apiBase}/api/admin/groups/group-1` && method === 'PATCH') {
        return jsonResponse({ id: 'group-1', name: 'engineering' });
      }

      if (url === `${apiBase}/api/admin/groups/group-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    await getGroup('group-1');
    await createGroup({ name: 'support', description: 'Support desk' });
    await updateGroup('group-1', { name: 'engineering', description: 'Engineering team' });
    await deleteGroup('group-1');

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/groups`,
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({ name: 'support', description: 'Support desk' }),
      })
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/groups/group-1`,
      expect.objectContaining({
        method: 'PATCH',
        body: JSON.stringify({ name: 'engineering', description: 'Engineering team' }),
      })
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/groups/group-1`,
      expect.objectContaining({ method: 'DELETE' })
    );
  });

  it('maps members and mappings calls to nested group endpoints', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/groups/group-1/members?page=2&pageSize=10` && method === 'GET') {
        return jsonResponse({ items: [], totalCount: 0, page: 2, pageSize: 10, totalPages: 0 });
      }

      if (url === `${apiBase}/api/admin/groups/group-1/members/user-1` && method === 'POST') {
        return Promise.resolve(new Response(null, { status: 201 }));
      }

      if (url === `${apiBase}/api/admin/groups/group-1/members/user-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      if (url === `${apiBase}/api/admin/groups/group-1/mappings` && method === 'GET') {
        return jsonResponse({ items: [] });
      }

      if (url === `${apiBase}/api/admin/groups/group-1/mappings` && method === 'POST') {
        return jsonResponse({ id: 'mapping-1', type: 'Role', value: 'role-1' }, 201);
      }

      if (url === `${apiBase}/api/admin/groups/group-1/mappings/mapping-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    await getGroupMembers('group-1', { page: 2, pageSize: 10 });
    await addMemberToGroup('group-1', 'user-1');
    await removeMemberFromGroup('group-1', 'user-1');
    await getGroupMappings('group-1');
    await addGroupMapping('group-1', { type: 'Role', value: 'role-1' });
    await removeGroupMapping('group-1', 'mapping-1');

    const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
    expect(calls).toContain(`GET ${apiBase}/api/admin/groups/group-1/members?page=2&pageSize=10`);
    expect(calls).toContain(`POST ${apiBase}/api/admin/groups/group-1/members/user-1`);
    expect(calls).toContain(`DELETE ${apiBase}/api/admin/groups/group-1/members/user-1`);
    expect(calls).toContain(`GET ${apiBase}/api/admin/groups/group-1/mappings`);
    expect(calls).toContain(`POST ${apiBase}/api/admin/groups/group-1/mappings`);
    expect(calls).toContain(`DELETE ${apiBase}/api/admin/groups/group-1/mappings/mapping-1`);
  });
});
