import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  addApplicationCertificate,
  addApplicationSecret,
  ApplicationClientType,
  ApplicationOptionAvailability,
  ApplicationProfile,
  configureApplicationOAuth,
  createApplication,
  deleteApplication,
  disableApplication,
  enableApplication,
  getApplication,
  getApplicationProfilePolicies,
  getApplications,
  listApplicationCredentials,
  revokeApplicationCredential,
  updateApplicationMetadata,
} from './applications-api';

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

describe('applications API client', () => {
  it('lists applications with unified filters and pagination', async () => {
    const response = { items: [], totalCount: 0, page: 2, pageSize: 25, totalPages: 0 };
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(response)));
    globalThis.fetch = fetchMock;

    await expect(getApplications({
      page: 2,
      pageSize: 25,
      search: 'orders',
      profile: ApplicationProfile.Web,
      status: 'Active',
      clientType: ApplicationClientType.Confidential,
    })).resolves.toStrictEqual(response);

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/applications?page=2&pageSize=25&search=orders&profile=Web&status=Active&clientType=Confidential`,
      expect.anything()
    );
  });

  it('uses only consolidated application lifecycle endpoints', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/applications/app-1` && method === 'GET') {
        return jsonResponse({ id: 'app-1' });
      }

      if (url === `${apiBase}/api/admin/applications` && method === 'POST') {
        return jsonResponse({ id: 'app-1', initialSecret: 'secret' });
      }

      if (url === `${apiBase}/api/admin/applications/app-1` && method === 'PATCH') {
        return jsonResponse({ id: 'app-1', displayName: 'Orders Admin' });
      }

      if (url === `${apiBase}/api/admin/applications/app-1/oauth` && method === 'PUT') {
        return jsonResponse({ id: 'app-1', profile: ApplicationProfile.MachineToMachine });
      }

      if (url === `${apiBase}/api/admin/applications/app-1/disable` && method === 'POST') {
        return jsonResponse({ id: 'app-1', status: 'Disabled' });
      }

      if (url === `${apiBase}/api/admin/applications/app-1/enable` && method === 'POST') {
        return jsonResponse({ id: 'app-1', status: 'Active' });
      }

      if (url === `${apiBase}/api/admin/applications/app-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    globalThis.fetch = fetchMock;

    await getApplication('app-1');
    await createApplication({
      clientId: 'orders-web',
      displayName: 'Orders Web',
      description: 'Orders UI',
      profile: ApplicationProfile.Web,
      clientType: ApplicationClientType.Confidential,
      allowedGrantTypes: ['authorization_code'],
      allowedScopes: ['openid', 'orders.read'],
      redirectUris: ['https://orders.example.com/callback'],
      postLogoutRedirectUris: [],
      requirePkce: true,
      requireConsent: true,
    });
    await updateApplicationMetadata('app-1', {
      displayName: 'Orders Admin',
      description: 'Updated',
    });
    await configureApplicationOAuth('app-1', {
      profile: ApplicationProfile.MachineToMachine,
      clientType: ApplicationClientType.Confidential,
      allowedGrantTypes: ['client_credentials'],
      allowedScopes: ['api'],
      redirectUris: [],
      postLogoutRedirectUris: [],
      requirePkce: false,
      requireConsent: false,
    });
    await disableApplication('app-1');
    await enableApplication('app-1');
    await deleteApplication('app-1');

    const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
    expect(calls).toEqual([
      `GET ${apiBase}/api/admin/applications/app-1`,
      `POST ${apiBase}/api/admin/applications`,
      `PATCH ${apiBase}/api/admin/applications/app-1`,
      `PUT ${apiBase}/api/admin/applications/app-1/oauth`,
      `POST ${apiBase}/api/admin/applications/app-1/disable`,
      `POST ${apiBase}/api/admin/applications/app-1/enable`,
      `DELETE ${apiBase}/api/admin/applications/app-1`,
    ]);
    expect(JSON.parse(fetchMock.mock.calls[1][1]?.body as string)).toMatchObject({ clientId: 'orders-web' });
  });

  it('uses consolidated application credential endpoints', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/applications/app-1/credentials` && method === 'GET') {
        return jsonResponse([]);
      }

      if (url === `${apiBase}/api/admin/applications/app-1/credentials/client-secrets` && method === 'POST') {
        return jsonResponse({ credentialId: 'cred-1', clientSecret: 'secret' });
      }

      if (url === `${apiBase}/api/admin/applications/app-1/credentials/certificates` && method === 'POST') {
        return jsonResponse({ credentialId: 'cred-2' });
      }

      if (url === `${apiBase}/api/admin/applications/app-1/credentials/cred-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    globalThis.fetch = fetchMock;

    await listApplicationCredentials('app-1');
    await addApplicationSecret('app-1', {
      description: 'Primary secret',
      expiresAt: '2026-06-24T12:00:00Z',
      revokeExisting: true,
    });
    await addApplicationCertificate('app-1', {
      thumbprint: 'ABC123',
      subject: 'CN=worker.example.com',
      description: 'Worker certificate',
      expiresAt: '2027-05-24T12:00:00Z',
    });
    await revokeApplicationCredential('app-1', 'cred-1');

    const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
    expect(calls).toEqual([
      `GET ${apiBase}/api/admin/applications/app-1/credentials`,
      `POST ${apiBase}/api/admin/applications/app-1/credentials/client-secrets`,
      `POST ${apiBase}/api/admin/applications/app-1/credentials/certificates`,
      `DELETE ${apiBase}/api/admin/applications/app-1/credentials/cred-1`,
    ]);
  });

  it('fetches profile policies with availability metadata', async () => {
    const response = [{
      applicationProfile: ApplicationProfile.Web,
      isSelectable: true,
      unavailabilityReason: null,
      defaultClientProfile: ApplicationClientType.Confidential,
      allowedClientProfiles: [ApplicationClientType.Confidential],
      allowedGrantTypes: ['authorization_code', 'refresh_token'],
      defaultGrantTypes: ['authorization_code'],
      options: {
        clientSecrets: ApplicationOptionAvailability.Available,
        clientCertificates: ApplicationOptionAvailability.Advanced,
        pkce: ApplicationOptionAvailability.Available,
      },
      requirePkce: false,
      defaultRequirePkce: true,
      defaultRequireConsent: true,
      requiresRedirectUris: true,
    }];
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(response)));
    globalThis.fetch = fetchMock;

    await expect(getApplicationProfilePolicies()).resolves.toStrictEqual(response);

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/applications/policies/profiles`,
      expect.anything()
    );
  });
});
