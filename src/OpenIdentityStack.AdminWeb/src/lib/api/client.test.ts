import { afterEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/config/runtime-config', () => ({
  getRuntimeConfig: (_key: string, fallback = '') => fallback,
}));

import { apiClient } from './client';

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
  apiClient.setTokenProvider(async () => null);
  apiClient.setLogoutHandler(null);
});

describe('ApiClient', () => {
  it('injects a bearer token after setting a token provider', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true })));
    globalThis.fetch = fetchMock;
    const tokenProvider = vi.fn().mockResolvedValue('token-123');
    apiClient.setTokenProvider(tokenProvider);

    await apiClient.get('/api/admin/example');

    expect(tokenProvider).toHaveBeenCalledOnce();
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer token-123');
  });

  it('triggers the logout handler and normalizes 401 errors', async () => {
    const logoutHandler = vi.fn();
    apiClient.setLogoutHandler(logoutHandler);
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          title: 'Unauthorized',
          detail: 'Token expired',
          type: 'https://example.com/errors/unauthorized',
        }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      )
    );

    await expect(apiClient.get('/api/admin/example')).rejects.toMatchObject({
      title: 'Unauthorized',
      detail: 'Token expired',
      status: 401,
      message: 'Token expired',
    });
    expect(logoutHandler).toHaveBeenCalledOnce();
  });

  it('preserves HTTP verb helpers', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ items: [1] })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'created' })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'updated' })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'patched' })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ success: true })));
    globalThis.fetch = fetchMock;

    await expect(apiClient.get('/users', { page: 2 })).resolves.toEqual({ items: [1] });
    await expect(apiClient.post('/users', { name: 'Alice' })).resolves.toEqual({ id: 'created' });
    await expect(apiClient.put('/users/1', { name: 'Bob' })).resolves.toEqual({ id: 'updated' });
    await expect(apiClient.patch('/users/1', { active: true })).resolves.toEqual({ id: 'patched' });
    await expect(apiClient.delete('/users/1')).resolves.toEqual({ success: true });

    expect(fetchMock.mock.calls[0][0]).toBe('http://localhost:5000/users?page=2');
    expect((fetchMock.mock.calls[1][1] as RequestInit).method).toBe('POST');
    expect((fetchMock.mock.calls[2][1] as RequestInit).method).toBe('PUT');
    expect((fetchMock.mock.calls[3][1] as RequestInit).method).toBe('PATCH');
    expect((fetchMock.mock.calls[4][1] as RequestInit).method).toBe('DELETE');
  });
});
