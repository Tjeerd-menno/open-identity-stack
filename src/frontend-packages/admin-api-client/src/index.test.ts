import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  createAdminApiClient,
  formatApiError,
  isApiError,
  type ApiError,
} from './index';

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
});

describe('admin api client', () => {
  it('offers reauthentication after acknowledgement expires without replaying again', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ errorCode: 'Forbidden.AdministrativeApproval.AcknowledgementRequired' }), { status: 403 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ errorCode: 'Forbidden.AdministrativeApproval.ReauthenticationRequired' }), { status: 403 }));
    globalThis.fetch = fetchMock;
    const approve = vi.fn().mockResolvedValue(true);
    const client = createAdminApiClient({ baseUrl: 'https://admin.example', onAdministrativeApprovalRequired: approve });
    await expect(client.post('/api/admin/users/user/roles/role')).rejects.toMatchObject({ errorCode: 'Forbidden.AdministrativeApproval.ReauthenticationRequired' });
    expect(approve).toHaveBeenCalledTimes(2);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('never treats a reauthentication callback as permission to replay a mutation', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ errorCode: 'Forbidden.AdministrativeApproval.ReauthenticationRequired' }), { status: 403 }));
    globalThis.fetch = fetchMock;
    const approve = vi.fn().mockResolvedValue(true);
    const client = createAdminApiClient({ baseUrl: 'https://admin.example', onAdministrativeApprovalRequired: approve });
    await expect(client.post('/api/admin/users/user/roles/role')).rejects.toMatchObject({ status: 403 });
    expect(approve).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it('retries an unrestricted mutation only after explicit approval', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        errorCode: 'Forbidden.AdministrativeApproval.AcknowledgementRequired',
      }), { status: 403 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    globalThis.fetch = fetchMock;
    const approve = vi.fn().mockResolvedValue(true);
    const client = createAdminApiClient({ baseUrl: 'https://admin.example', onAdministrativeApprovalRequired: approve });

    await client.post('/api/admin/users/user/roles/role');

    expect(approve).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(new Headers(fetchMock.mock.calls[0][1].headers).has('X-OIS-Administrative-Approval')).toBe(false);
    expect(new Headers(fetchMock.mock.calls[1][1].headers).get('X-OIS-Administrative-Approval')).toBe('acknowledge');
  });

  it('appends query parameters and omits undefined values', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true })));
    globalThis.fetch = fetchMock;
    const client = createAdminApiClient({ baseUrl: 'https://admin.example' });

    await client.get('/api/admin/users', { page: 2, search: 'alice', status: undefined });

    expect(fetchMock).toHaveBeenCalledWith(
      'https://admin.example/api/admin/users?page=2&search=alice',
      expect.any(Object)
    );
  });

  it('injects Bearer token when getAccessToken returns a token', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true })));
    globalThis.fetch = fetchMock;
    const client = createAdminApiClient({
      baseUrl: 'https://admin.example',
      getAccessToken: async () => 'token-123',
    });

    await client.get('/api/admin/users');

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer token-123');
  });

  it('does not add Authorization when no token is available', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true })));
    globalThis.fetch = fetchMock;
    const client = createAdminApiClient({
      baseUrl: 'https://admin.example',
      getAccessToken: async () => null,
    });

    await client.get('/api/admin/users');

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(new Headers(init.headers).has('Authorization')).toBe(false);
  });

  it('serializes JSON request bodies', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'created' })));
    globalThis.fetch = fetchMock;
    const client = createAdminApiClient({ baseUrl: 'https://admin.example' });

    await client.post('/api/admin/users', { email: 'alice@example.com' });

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect(init.method).toBe('POST');
    expect(new Headers(init.headers).get('Content-Type')).toBe('application/json');
    expect(init.body).toBe(JSON.stringify({ email: 'alice@example.com' }));
  });

  it('parses JSON responses', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'user-1' })));
    const client = createAdminApiClient({ baseUrl: 'https://admin.example' });

    await expect(client.get<{ id: string }>('/api/admin/users/user-1')).resolves.toEqual({ id: 'user-1' });
  });

  it('returns undefined for HTTP 204 and empty bodies', async () => {
    const client = createAdminApiClient({ baseUrl: 'https://admin.example' });
    globalThis.fetch = vi.fn().mockResolvedValueOnce(new Response(null, { status: 204 }));
    await expect(client.delete('/api/admin/users/user-1')).resolves.toBeUndefined();

    globalThis.fetch = vi.fn().mockResolvedValueOnce(new Response('', { status: 200 }));
    await expect(client.get('/api/admin/users/user-1')).resolves.toBeUndefined();
  });

  it('calls onUnauthorized for HTTP 401', async () => {
    const onUnauthorized = vi.fn();
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized' }), {
        status: 401,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    const client = createAdminApiClient({ baseUrl: 'https://admin.example', onUnauthorized });

    await expect(client.get('/api/admin/users')).rejects.toMatchObject({ status: 401 });

    expect(onUnauthorized).toHaveBeenCalledOnce();
  });

  it('normalizes problem-details errors', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          type: 'validation',
          title: 'Validation failed',
          status: 422,
          detail: 'Check the request.',
          errors: { DisplayName: ['Required'] },
        }),
        { status: 422, headers: { 'Content-Type': 'application/json' } }
      )
    );
    const client = createAdminApiClient({ baseUrl: 'https://admin.example' });

    await expect(client.post('/api/admin/users', {})).rejects.toMatchObject({
      type: 'validation',
      title: 'Validation failed',
      status: 422,
      detail: 'Check the request.',
      errors: { DisplayName: ['Required'] },
    });
  });

  it('retains the Problem Details code extension as errorCode', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          type: 'https://example.test/problems/administrative-approval',
          title: 'Administrative approval required',
          status: 403,
          detail: 'Acknowledge the operation before continuing.',
          code: 'Forbidden.AdministrativeApproval.AcknowledgementRequired',
        }),
        { status: 403, headers: { 'Content-Type': 'application/problem+json' } }
      )
    );
    const client = createAdminApiClient({ baseUrl: 'https://admin.example' });

    await expect(client.post('/api/admin/applications', {})).rejects.toMatchObject({
      status: 403,
      errorCode: 'Forbidden.AdministrativeApproval.AcknowledgementRequired',
    });
  });

  it('normalizes simple error payloads', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ error: 'duplicate_user', message: 'User already exists.' }), {
        status: 409,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    const client = createAdminApiClient({ baseUrl: 'https://admin.example' });

    await expect(client.post('/api/admin/users', {})).rejects.toMatchObject({
      status: 409,
      title: 'Admin API request failed',
      detail: 'User already exists.',
      errorCode: 'duplicate_user',
    });
  });

  it('reports network failures as ApiError', async () => {
    globalThis.fetch = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    const client = createAdminApiClient({ baseUrl: 'https://admin.example' });

    await expect(client.get('/api/admin/users')).rejects.toMatchObject({
      type: 'network-error',
      title: 'Network Error',
      status: 0,
    });
  });

  it('formats ApiError instances', () => {
    const error: ApiError = Object.assign(new Error('Check the request.'), {
      type: 'validation',
      title: 'Validation failed',
      status: 422,
      detail: 'Check the request.',
      errors: { DisplayName: ['Required'] },
    });

    expect(isApiError(error)).toBe(true);
    expect(formatApiError(error)).toBe('Check the request.\nDisplayName: Required');
  });
});
