import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderManagementWeb } from '@/test/render';
import { UsersPage } from './UsersPage';

const apiBase = 'http://localhost:5000';

function jsonResponse(body: unknown) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  }));
}

describe('UsersPage', () => {
  it('lists, inspects, edits, disables, and assigns an existing role to a user', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/users?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [
            {
              id: 'user-1',
              email: 'ada@example.com',
              displayName: 'Ada Lovelace',
              status: 'Active',
              createdAt: '2026-01-01T00:00:00Z',
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/users?page=1&pageSize=20&search=ada` && method === 'GET') {
        return jsonResponse({
          items: [
            {
              id: 'user-1',
              email: 'ada@example.com',
              displayName: 'Ada Lovelace',
              status: 'Active',
              createdAt: '2026-01-01T00:00:00Z',
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/users/user-1` && method === 'GET') {
        return jsonResponse({
          id: 'user-1',
          email: 'ada@example.com',
          displayName: 'Ada Lovelace',
          status: 'Active',
          mfaEnabled: true,
          lastLoginAt: null,
          createdAt: '2026-01-01T00:00:00Z',
          modifiedAt: null,
          profile: {},
        });
      }

      if (url === `${apiBase}/api/admin/users/user-1/roles` && method === 'GET') {
        return jsonResponse({ userId: 'user-1', roles: [] });
      }

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=100` && method === 'GET') {
        return jsonResponse({
          items: [
            {
              id: 'role-1',
              name: 'support',
              displayName: 'Support operator',
              isSystemRole: false,
              isActive: true,
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 100,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/users/user-1` && method === 'PUT') {
        return jsonResponse({ userId: 'user-1', updatedAt: '2026-01-02T00:00:00Z' });
      }

      if (url === `${apiBase}/api/admin/users` && method === 'POST') {
        return jsonResponse({
          id: 'user-2',
          email: 'grace@example.com',
          displayName: 'Grace Hopper',
          status: 'PendingVerification',
          createdAt: '2026-01-02T00:00:00Z',
        });
      }

      if (url === `${apiBase}/api/admin/users/user-1/disable` && method === 'POST') {
        return jsonResponse({ userId: 'user-1', timestamp: '2026-01-02T00:00:00Z' });
      }

      if (url === `${apiBase}/api/admin/users/user-1/roles/role-1` && method === 'POST') {
        return jsonResponse({});
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<UsersPage />);

    await user.type(await screen.findByLabelText(/search users/i), 'ada');
    await user.click(screen.getByRole('button', { name: /search/i }));

    await user.click(screen.getByRole('button', { name: /create user/i }));
    await user.type(screen.getByLabelText(/^email$/i), 'grace@example.com');
    await user.type(screen.getByLabelText(/new display name/i), 'Grace Hopper');
    await user.type(screen.getByLabelText(/^password$/i), 'Pass1234!');
    await user.click(screen.getByRole('button', { name: /save new user/i }));

    await user.click(await screen.findByRole('button', { name: /ada lovelace/i }));
    expect(await screen.findByRole('heading', { name: /ada lovelace/i })).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: /user details/i })).getByText('ada@example.com')).toBeInTheDocument();

    await user.clear(screen.getByLabelText(/display name/i));
    await user.type(screen.getByLabelText(/display name/i), 'Ada Byron');
    await user.click(screen.getByRole('button', { name: /save changes/i }));

    await user.click(screen.getByRole('button', { name: /disable user/i }));

    const roleSelect = await screen.findByLabelText(/assign role/i);
    await user.selectOptions(roleSelect, 'role-1');
    await user.click(screen.getByRole('button', { name: /assign selected role/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/users/user-1`,
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify({ displayName: 'Ada Byron' }),
        })
      );
    });

    const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
    expect(calls).toContain(`POST ${apiBase}/api/admin/users/user-1/disable`);
    expect(calls).toContain(`POST ${apiBase}/api/admin/users/user-1/roles/role-1`);
    expect(calls).toContain(`POST ${apiBase}/api/admin/users`);
    expect(within(screen.getByRole('region', { name: /user details/i })).getByText(/mfa enabled/i)).toBeInTheDocument();
  });

  it('navigates between pages when pagination controls are used', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/users?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [
            {
              id: 'user-1',
              email: 'ada@example.com',
              displayName: 'Ada Lovelace',
              status: 'Active',
              createdAt: '2026-01-01T00:00:00Z',
            },
          ],
          totalCount: 50,
          page: 1,
          pageSize: 20,
          totalPages: 3,
        });
      }

      if (url === `${apiBase}/api/admin/users?page=2&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [
            {
              id: 'user-2',
              email: 'grace@example.com',
              displayName: 'Grace Hopper',
              status: 'Active',
              createdAt: '2026-01-02T00:00:00Z',
            },
          ],
          totalCount: 50,
          page: 2,
          pageSize: 20,
          totalPages: 3,
        });
      }

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=100` && method === 'GET') {
        return jsonResponse({
          items: [],
          totalCount: 0,
          page: 1,
          pageSize: 100,
          totalPages: 0,
        });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<UsersPage />);

    await screen.findByRole('button', { name: /ada lovelace/i });
    expect(screen.queryByRole('button', { name: /grace hopper/i })).not.toBeInTheDocument();

    const nextPageButton = screen.getByRole('button', { name: '2' });
    await user.click(nextPageButton);

    await screen.findByRole('button', { name: /grace hopper/i });
    expect(screen.queryByRole('button', { name: /ada lovelace/i })).not.toBeInTheDocument();

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/users?page=2&pageSize=20`,
        expect.anything()
      );
    });
  });

  it('resets pagination to page 1 when search term changes', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/users?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [
            {
              id: 'user-1',
              email: 'ada@example.com',
              displayName: 'Ada Lovelace',
              status: 'Active',
              createdAt: '2026-01-01T00:00:00Z',
            },
          ],
          totalCount: 50,
          page: 1,
          pageSize: 20,
          totalPages: 3,
        });
      }

      if (url === `${apiBase}/api/admin/users?page=1&pageSize=20&search=grace` && method === 'GET') {
        return jsonResponse({
          items: [
            {
              id: 'user-2',
              email: 'grace@example.com',
              displayName: 'Grace Hopper',
              status: 'Active',
              createdAt: '2026-01-02T00:00:00Z',
            },
          ],
          totalCount: 5,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=100` && method === 'GET') {
        return jsonResponse({
          items: [],
          totalCount: 0,
          page: 1,
          pageSize: 100,
          totalPages: 0,
        });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<UsersPage />);

    await screen.findByRole('button', { name: /ada lovelace/i });

    const searchInput = screen.getByLabelText(/search users/i);
    await user.type(searchInput, 'grace');
    await user.click(screen.getByRole('button', { name: /search/i }));

    await screen.findByRole('button', { name: /grace hopper/i });
    expect(screen.queryByRole('button', { name: /ada lovelace/i })).not.toBeInTheDocument();

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/users?page=1&pageSize=20&search=grace`,
        expect.anything()
      );
    });
  });

  it('blocks privileged user actions when the operator lacks write permissions', async () => {
    vi.stubGlobal('fetch', vi.fn(() => jsonResponse({
      items: [
        {
          id: 'user-1',
          email: 'ada@example.com',
          displayName: 'Ada Lovelace',
          status: 'Active',
          createdAt: '2026-01-01T00:00:00Z',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    })));

    renderManagementWeb(<UsersPage permissions={['users:read']} />);

    expect(await screen.findByText(/read-only access/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /create user/i })).not.toBeInTheDocument();
  });

  it('shows recoverable operation failures near the failed action', async () => {
    const user = userEvent.setup();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/users?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [
            {
              id: 'user-1',
              email: 'ada@example.com',
              displayName: 'Ada Lovelace',
              status: 'Active',
              createdAt: '2026-01-01T00:00:00Z',
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/users/user-1` && method === 'GET') {
        return jsonResponse({
          id: 'user-1',
          email: 'ada@example.com',
          displayName: 'Ada Lovelace',
          status: 'Active',
          mfaEnabled: true,
          lastLoginAt: null,
          createdAt: '2026-01-01T00:00:00Z',
          modifiedAt: null,
          profile: {},
        });
      }

      if (url === `${apiBase}/api/admin/users/user-1/roles` && method === 'GET') {
        return jsonResponse({ userId: 'user-1', roles: [] });
      }

      if (url === `${apiBase}/api/admin/roles?page=1&pageSize=100` && method === 'GET') {
        return jsonResponse({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 });
      }

      if (url === `${apiBase}/api/admin/users/user-1` && method === 'PUT') {
        return Promise.resolve(new Response(JSON.stringify({ detail: 'Display name is required.' }), { status: 400 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }));

    renderManagementWeb(<UsersPage />);

    await user.click(await screen.findByRole('button', { name: /ada lovelace/i }));
    await user.clear(await screen.findByLabelText(/display name/i));
    await user.click(screen.getByRole('button', { name: /save changes/i }));

    expect(await screen.findByText(/display name is required/i)).toBeInTheDocument();
  });
});
