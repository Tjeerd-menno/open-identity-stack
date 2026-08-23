import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, setAccessTokenProvider, setUnauthorizedHandler } from './api';

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
  setAccessTokenProvider(async () => null);
  setUnauthorizedHandler(null);
});

describe('Management Web API adapter', () => {
  it('uses the access token provider for typed Admin API calls', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 0 }))
    );
    globalThis.fetch = fetchMock;
    setAccessTokenProvider(async () => 'opaque-access-token');

    await api.users.getUsers({ page: 1 });

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(fetchMock).toHaveBeenCalledWith('http://localhost:5000/api/admin/users?page=1', expect.any(Object));
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer opaque-access-token');
  });

  it('routes unauthorized responses to the registered handler', async () => {
    const unauthorized = vi.fn();
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized' }), { status: 401 })
    );
    setUnauthorizedHandler(unauthorized);

    await expect(api.users.getUsers()).rejects.toMatchObject({ status: 401 });

    expect(unauthorized).toHaveBeenCalledOnce();
  });
});
