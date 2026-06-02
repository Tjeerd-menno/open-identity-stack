import {
  getAuthenticationProviders,
  getAuthenticationSettings,
  setDefaultProvider,
  setLocalFallback,
} from './settings-api';

const apiBase = 'http://localhost:5000';

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

describe('settings-api', () => {
  it('maps authentication settings calls to the backend contract', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/authentication-settings` && method === 'GET') {
        return jsonResponse({
          defaultProviderId: 'local',
          isLocalDefault: true,
          localFallbackEnabled: true,
          updatedAt: '2026-01-01T00:00:00Z',
        });
      }

      if (url === `${apiBase}/api/admin/authentication-settings/providers` && method === 'GET') {
        return jsonResponse([{ id: 'local', name: 'local', displayName: 'Local Accounts', type: 'local', isActive: true }]);
      }

      if (url === `${apiBase}/api/admin/authentication-settings/default-provider` && method === 'PUT') {
        return jsonResponse({
          defaultProviderId: 'provider-1',
          isLocalDefault: false,
          localFallbackEnabled: true,
          updatedAt: '2026-01-02T00:00:00Z',
        });
      }

      if (url === `${apiBase}/api/admin/authentication-settings/local-fallback` && method === 'PUT') {
        return jsonResponse({
          defaultProviderId: 'provider-1',
          isLocalDefault: false,
          localFallbackEnabled: false,
          updatedAt: '2026-01-03T00:00:00Z',
        });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    await getAuthenticationSettings();
    await getAuthenticationProviders();
    await setDefaultProvider({ providerId: 'provider-1' });
    await setLocalFallback({ enabled: false });

    const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
    expect(calls).toContain(`GET ${apiBase}/api/admin/authentication-settings`);
    expect(calls).toContain(`GET ${apiBase}/api/admin/authentication-settings/providers`);
    expect(calls).toContain(`PUT ${apiBase}/api/admin/authentication-settings/default-provider`);
    expect(calls).toContain(`PUT ${apiBase}/api/admin/authentication-settings/local-fallback`);
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/authentication-settings/default-provider`,
      expect.objectContaining({ method: 'PUT', body: '{"providerId":"provider-1"}' })
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/authentication-settings/local-fallback`,
      expect.objectContaining({ method: 'PUT', body: '{"enabled":false}' })
    );
  });
});
