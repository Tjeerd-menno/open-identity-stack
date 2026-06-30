import {
  createProvider,
  deleteProvider,
  disableProvider,
  enableProvider,
  getProvider,
  getProviders,
  updateProvider,
} from './providers-api';

const apiBase = 'http://localhost:5000';

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

describe('providers-api', () => {
  it('uses the consolidated admin providers list endpoint with disabled providers included', async () => {
    const fetchMock = vi.fn(() => jsonResponse([]));
    vi.stubGlobal('fetch', fetchMock);

    await getProviders(true);

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/providers?includeDisabled=true`,
      expect.anything()
    );
  });

  it('maps provider detail, mutation, status, and delete calls to the backend contract', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/providers/provider-1` && method === 'GET') {
        return jsonResponse({ id: 'provider-1', status: 'Active' });
      }

      if (url === `${apiBase}/api/admin/providers` && method === 'POST') {
        return jsonResponse({ id: 'provider-2', status: 'Active' }, 201);
      }

      if (url === `${apiBase}/api/admin/providers/provider-1` && method === 'PATCH') {
        return jsonResponse({ id: 'provider-1', status: 'Disabled' });
      }

      if (url === `${apiBase}/api/admin/providers/provider-1/enable` && method === 'POST') {
        return jsonResponse({ id: 'provider-1', status: 'Active' });
      }

      if (url === `${apiBase}/api/admin/providers/provider-1/disable` && method === 'POST') {
        return jsonResponse({ id: 'provider-1', status: 'Disabled' });
      }

      if (url === `${apiBase}/api/admin/providers/provider-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    await getProvider('provider-1');
    await createProvider({
      name: 'google',
      displayName: 'Google Login',
      authority: 'https://accounts.google.com',
      clientId: 'google-client',
      clientSecret: 'secret',
      scopes: ['openid', 'profile', 'email'],
      jitProvisioningEnabled: true,
    });
    await updateProvider('provider-1', {
      displayName: 'Google Workspace',
      clientId: 'updated-client',
      clientSecret: 'new-secret',
      scopes: ['openid', 'email'],
      jitProvisioningEnabled: false,
    });
    await enableProvider('provider-1');
    await disableProvider('provider-1');
    await deleteProvider('provider-1');

    const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
    expect(calls).toContain(`GET ${apiBase}/api/admin/providers/provider-1`);
    expect(calls).toContain(`POST ${apiBase}/api/admin/providers`);
    expect(calls).toContain(`PATCH ${apiBase}/api/admin/providers/provider-1`);
    expect(calls).toContain(`POST ${apiBase}/api/admin/providers/provider-1/enable`);
    expect(calls).toContain(`POST ${apiBase}/api/admin/providers/provider-1/disable`);
    expect(calls).toContain(`DELETE ${apiBase}/api/admin/providers/provider-1`);
  });
});
