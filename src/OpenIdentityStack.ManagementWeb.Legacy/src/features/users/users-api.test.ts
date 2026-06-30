import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  assignUserRole,
  createUser,
  deleteUser,
  disableUser,
  enableUser,
  getUser,
  getUserGroups,
  getUserRoles,
  getUsers,
  getUserUpstreamIdentities,
  linkUserUpstreamIdentity,
  resetUserPassword,
  unassignUserRole,
  unlinkUserUpstreamIdentity,
  updateUser,
} from './users-api';

const apiBase = 'http://localhost:5000';
const originalFetch = globalThis.fetch;

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

afterEach(() => {
  globalThis.fetch = originalFetch;
});

describe('users API client', () => {
  it('lists users with pagination and search', async () => {
    const response = { items: [], totalCount: 0, page: 2, pageSize: 25, totalPages: 0 };
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(response)));
    globalThis.fetch = fetchMock;

    await expect(getUsers({ page: 2, pageSize: 25, search: 'ada' })).resolves.toStrictEqual(response);

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/users?page=2&pageSize=25&search=ada`,
      expect.anything()
    );
  });

  it('uses ManagementWeb user lifecycle endpoints', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/users/user-1` && method === 'GET') {
        return jsonResponse({ id: 'user-1' });
      }

      if (url === `${apiBase}/api/admin/users` && method === 'POST') {
        return jsonResponse({ id: 'user-2' }, 201);
      }

      if (url === `${apiBase}/api/admin/users/user-1` && method === 'PUT') {
        return jsonResponse({ id: 'user-1', displayName: 'Ada Byron' });
      }

      if (url === `${apiBase}/api/admin/users/user-1/disable` && method === 'POST') {
        return jsonResponse({ userId: 'user-1', status: 'Disabled' });
      }

      if (url === `${apiBase}/api/admin/users/user-1/enable` && method === 'POST') {
        return jsonResponse({ userId: 'user-1', status: 'Active' });
      }

      if (url === `${apiBase}/api/admin/users/user-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    globalThis.fetch = fetchMock;

    await getUser('user-1');
    await createUser({ email: 'ada@example.com', displayName: 'Ada Lovelace', password: 'Pass1234!' });
    await updateUser('user-1', { displayName: 'Ada Byron' });
    await disableUser('user-1', { reason: 'Operator request' });
    await enableUser('user-1');
    await deleteUser('user-1');

    const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
    expect(calls).toEqual([
      `GET ${apiBase}/api/admin/users/user-1`,
      `POST ${apiBase}/api/admin/users`,
      `PUT ${apiBase}/api/admin/users/user-1`,
      `POST ${apiBase}/api/admin/users/user-1/disable`,
      `POST ${apiBase}/api/admin/users/user-1/enable`,
      `DELETE ${apiBase}/api/admin/users/user-1`,
    ]);
    expect(JSON.parse(fetchMock.mock.calls[2][1]?.body as string)).toEqual({ displayName: 'Ada Byron' });
  });

  it('supports password reset, roles, groups, and upstream identities', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/users/user-1/reset-password` && method === 'POST') {
        return jsonResponse({ userId: 'user-1', temporaryPassword: 'Temp1234!' });
      }

      if (url === `${apiBase}/api/admin/users/user-1/roles` && method === 'GET') {
        return jsonResponse({ userId: 'user-1', roles: [] });
      }

      if (url === `${apiBase}/api/admin/users/user-1/roles/role-1` && method === 'POST') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      if (url === `${apiBase}/api/admin/users/user-1/roles/role-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      if (url === `${apiBase}/api/admin/users/user-1/groups` && method === 'GET') {
        return jsonResponse([]);
      }

      if (url === `${apiBase}/api/admin/users/user-1/upstream-identities` && method === 'GET') {
        return jsonResponse({ items: [] });
      }

      if (url === `${apiBase}/api/admin/users/user-1/upstream-identities` && method === 'POST') {
        return jsonResponse({ providerId: 'oidc', subject: 'sub-1' }, 201);
      }

      if (url === `${apiBase}/api/admin/users/user-1/upstream-identities/oidc` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    globalThis.fetch = fetchMock;

    await resetUserPassword('user-1', { newPassword: 'Temp1234!' });
    await getUserRoles('user-1');
    await assignUserRole('user-1', 'role-1');
    await unassignUserRole('user-1', 'role-1');
    await getUserGroups('user-1');
    await getUserUpstreamIdentities('user-1');
    await linkUserUpstreamIdentity('user-1', { providerId: 'oidc', subject: 'sub-1' });
    await unlinkUserUpstreamIdentity('user-1', 'oidc');

    const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
    expect(calls).toEqual([
      `POST ${apiBase}/api/admin/users/user-1/reset-password`,
      `GET ${apiBase}/api/admin/users/user-1/roles`,
      `POST ${apiBase}/api/admin/users/user-1/roles/role-1`,
      `DELETE ${apiBase}/api/admin/users/user-1/roles/role-1`,
      `GET ${apiBase}/api/admin/users/user-1/groups`,
      `GET ${apiBase}/api/admin/users/user-1/upstream-identities`,
      `POST ${apiBase}/api/admin/users/user-1/upstream-identities`,
      `DELETE ${apiBase}/api/admin/users/user-1/upstream-identities/oidc`,
    ]);
  });
});
