import {
  getSession,
  getSessions,
  revokeAllUserSessions,
  revokeSession,
} from './sessions-api';

const apiBase = 'http://localhost:5000';

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

describe('sessions-api', () => {
  it('uses the admin sessions list endpoint with pagination, search, and status filters', async () => {
    const fetchMock = vi.fn(() => jsonResponse({
      items: [],
      totalCount: 0,
      page: 2,
      pageSize: 25,
      totalPages: 0,
    }));
    vi.stubGlobal('fetch', fetchMock);

    await getSessions({ page: 2, pageSize: 25, search: '10.0.0.5', status: 'Active' });

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/sessions?page=2&pageSize=25&search=10.0.0.5&status=Active`,
      expect.anything()
    );
  });

  it('maps session detail and revocation calls to the backend contract', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/sessions/session-1` && method === 'GET') {
        return jsonResponse({ id: 'session-1', status: 'Active' });
      }

      if (url === `${apiBase}/api/admin/sessions/session-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      if (url === `${apiBase}/api/admin/users/user-1/sessions` && method === 'DELETE') {
        return jsonResponse({ revokedCount: 2, revokedAt: '2026-01-02T00:00:00Z' });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    await getSession('session-1');
    await revokeSession('session-1');
    await revokeAllUserSessions('user-1');

    const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
    expect(calls).toContain(`GET ${apiBase}/api/admin/sessions/session-1`);
    expect(calls).toContain(`DELETE ${apiBase}/api/admin/sessions/session-1`);
    expect(calls).toContain(`DELETE ${apiBase}/api/admin/users/user-1/sessions`);
  });
});
