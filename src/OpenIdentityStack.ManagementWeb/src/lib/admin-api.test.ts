import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  getApiErrorMessage,
  isApiError,
  request,
  setAccessTokenProvider,
  setUnauthorizedHandler,
} from './admin-api';

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
  setAccessTokenProvider(async () => null);
  setUnauthorizedHandler(null);
});

describe('admin api foundation', () => {
  it('injects bearer tokens into admin api requests', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true })));
    globalThis.fetch = fetchMock;
    setAccessTokenProvider(async () => 'access-token');

    await request<{ ok: boolean }>('/api/admin/example');

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer access-token');
  });

  it('normalizes problem details validation errors', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          title: 'Validation failed',
          status: 422,
          detail: 'Check the request.',
          errors: { DisplayName: ['Required'] },
        }),
        { status: 422, headers: { 'Content-Type': 'application/json' } }
      )
    );

    await expect(request('/api/admin/example')).rejects.toMatchObject({
      title: 'Validation failed',
      status: 422,
      detail: 'Check the request.',
      errors: { DisplayName: ['Required'] },
    });
  });

  it('calls unauthorized handler for 401 responses', async () => {
    const unauthorizedHandler = vi.fn();
    setUnauthorizedHandler(unauthorizedHandler);
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized' }), {
        status: 401,
        headers: { 'Content-Type': 'application/json' },
      })
    );

    await expect(request('/api/admin/example')).rejects.toMatchObject({ status: 401 });

    expect(unauthorizedHandler).toHaveBeenCalledTimes(1);
  });

  it('formats normalized api errors for display', () => {
    const error = {
      type: 'validation',
      title: 'Validation failed',
      status: 422,
      detail: 'Check the request.',
      errors: { DisplayName: ['Required'] },
    };

    expect(isApiError(error)).toBe(true);
    expect(getApiErrorMessage(error)).toContain('DisplayName: Required');
  });
});
