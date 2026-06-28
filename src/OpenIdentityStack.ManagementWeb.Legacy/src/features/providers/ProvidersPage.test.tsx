import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderManagementWeb } from '@/test/render';
import { ProvidersPage } from './ProvidersPage';

const apiBase = 'http://localhost:5000';

let googleProvider = {
  id: 'provider-1',
  name: 'google',
  displayName: 'Google Login',
  authority: 'https://accounts.google.com',
  clientId: 'google-client',
  scopes: ['openid', 'profile', 'email'],
  jitProvisioningEnabled: true,
  status: 'Active',
  createdAt: '2026-01-01T00:00:00Z',
};

const contosoProvider = {
  id: 'provider-2',
  name: 'contoso',
  displayName: 'Contoso IdP',
  authority: 'https://login.contoso.test',
  clientId: 'contoso-client',
  scopes: ['openid', 'email'],
  jitProvisioningEnabled: false,
  status: 'Disabled',
  createdAt: '2026-01-02T00:00:00Z',
};

let settings = {
  defaultProviderId: 'local',
  isLocalDefault: true,
  localFallbackEnabled: true,
  updatedAt: '2026-01-01T00:00:00Z',
};

const authenticationProviders = [
  { id: 'local', name: 'local', displayName: 'Local Accounts', type: 'local', isActive: true },
  { id: 'provider-1', name: 'google', displayName: 'Google Login', type: 'external', isActive: true },
];

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

function setupProviderFetch() {
  googleProvider = {
    ...googleProvider,
    displayName: 'Google Login',
    clientId: 'google-client',
    scopes: ['openid', 'profile', 'email'],
    jitProvisioningEnabled: true,
    status: 'Active',
  };
  settings = {
    defaultProviderId: 'local',
    isLocalDefault: true,
    localFallbackEnabled: true,
    updatedAt: '2026-01-01T00:00:00Z',
  };

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = input.toString();
    const method = init?.method ?? 'GET';

    if (url === `${apiBase}/api/admin/providers?includeDisabled=true` && method === 'GET') {
      return jsonResponse([googleProvider, contosoProvider]);
    }

    if (url === `${apiBase}/api/admin/providers/provider-1` && method === 'GET') {
      return jsonResponse(googleProvider);
    }

    if (url === `${apiBase}/api/admin/providers` && method === 'POST') {
      return jsonResponse({
        id: 'provider-3',
        name: 'okta',
        displayName: 'Okta Login',
        authority: 'https://okta.example.com',
        clientId: 'okta-client',
        scopes: ['openid', 'profile'],
        jitProvisioningEnabled: true,
        status: 'Active',
        createdAt: '2026-01-03T00:00:00Z',
      }, 201);
    }

    if (url === `${apiBase}/api/admin/providers/provider-1` && method === 'PATCH') {
      const body = JSON.parse(init?.body?.toString() ?? '{}') as Partial<typeof googleProvider>;
      googleProvider = { ...googleProvider, ...body };
      return jsonResponse(googleProvider);
    }

    if (url === `${apiBase}/api/admin/providers/provider-1/disable` && method === 'POST') {
      googleProvider = { ...googleProvider, status: 'Disabled' };
      return jsonResponse(googleProvider);
    }

    if (url === `${apiBase}/api/admin/providers/provider-1/enable` && method === 'POST') {
      googleProvider = { ...googleProvider, status: 'Active' };
      return jsonResponse(googleProvider);
    }

    if (url === `${apiBase}/api/admin/providers/provider-1` && method === 'DELETE') {
      return Promise.resolve(new Response(null, { status: 204 }));
    }

    if (url === `${apiBase}/api/admin/authentication-settings` && method === 'GET') {
      return jsonResponse(settings);
    }

    if (url === `${apiBase}/api/admin/authentication-settings/providers` && method === 'GET') {
      return jsonResponse(authenticationProviders);
    }

    if (url === `${apiBase}/api/admin/authentication-settings/default-provider` && method === 'PUT') {
      const body = JSON.parse(init?.body?.toString() ?? '{}') as { providerId: string };
      settings = {
        ...settings,
        defaultProviderId: body.providerId,
        isLocalDefault: body.providerId === 'local',
      };
      return jsonResponse(settings);
    }

    if (url === `${apiBase}/api/admin/authentication-settings/local-fallback` && method === 'PUT') {
      const body = JSON.parse(init?.body?.toString() ?? '{}') as { enabled: boolean };
      settings = {
        ...settings,
        localFallbackEnabled: body.enabled,
      };
      return jsonResponse(settings);
    }

    return Promise.resolve(new Response(null, { status: 404 }));
  });

  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('ProvidersPage', () => {
  it('lists, searches, creates, views, edits, toggles, and deletes identity providers', async () => {
    const user = userEvent.setup();
    const fetchMock = setupProviderFetch();

    const { unmount } = renderManagementWeb(
      <ProvidersPage permissions={['providers:read', 'providers:write', 'providers:delete']} />,
      { initialEntries: ['/providers'] }
    );

    expect(await screen.findByRole('heading', { name: /^identity providers$/i })).toBeInTheDocument();
    expect(await screen.findByText('Google Login')).toBeInTheDocument();
    expect(screen.getByText('https://accounts.google.com')).toBeInTheDocument();
    expect(screen.getAllByText('OIDC')[0]).toBeInTheDocument();

    await user.type(screen.getByLabelText(/search providers/i), 'contoso');
    await waitFor(() => {
      expect(screen.queryByText('Google Login')).not.toBeInTheDocument();
    });
    expect(screen.getByText('Contoso IdP')).toBeInTheDocument();

    await user.clear(screen.getByLabelText(/search providers/i));
    await screen.findByText('Google Login');

    await user.click(screen.getByRole('button', { name: /new provider/i }));
    await screen.findByRole('heading', { name: /create provider/i });
    await user.type(screen.getByLabelText(/^provider name/i), 'okta');
    await user.type(screen.getByLabelText(/display name/i), 'Okta Login');
    await user.type(screen.getByLabelText(/authority/i), 'https://okta.example.com');
    await user.type(screen.getByLabelText(/client id/i), 'okta-client');
    await user.type(screen.getByLabelText(/client secret/i), 'okta-secret');
    await user.clear(screen.getByLabelText(/scopes/i));
    await user.type(screen.getByLabelText(/scopes/i), 'openid profile');
    await user.click(screen.getByRole('button', { name: /create provider/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/providers`,
        expect.objectContaining({
          method: 'POST',
          body: expect.stringContaining('"scopes":["openid","profile"]'),
        })
      );
    });

    unmount();

    renderManagementWeb(
      <ProvidersPage permissions={['providers:read', 'providers:write', 'providers:delete']} />,
      { initialEntries: ['/providers/provider-1'] }
    );

    expect(await screen.findByRole('heading', { name: /google login/i })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: /provider details/i })).toBeInTheDocument();
    expect(screen.getByText(/client id: google-client/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /disable provider/i }));
    await waitFor(() => {
      const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
      expect(calls).toContain(`POST ${apiBase}/api/admin/providers/provider-1/disable`);
    });

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /enable provider/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /enable provider/i }));
    await waitFor(() => {
      const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
      expect(calls).toContain(`POST ${apiBase}/api/admin/providers/provider-1/enable`);
    });

    await user.click(screen.getByRole('button', { name: /edit provider/i }));
    await screen.findByRole('heading', { name: /edit provider/i });
    await user.clear(screen.getByLabelText(/display name/i));
    await user.type(screen.getByLabelText(/display name/i), 'Google Workspace');
    await user.clear(screen.getByLabelText(/client id/i));
    await user.type(screen.getByLabelText(/client id/i), 'workspace-client');
    await user.clear(screen.getByLabelText(/scopes/i));
    await user.type(screen.getByLabelText(/scopes/i), 'openid email');
    await user.click(screen.getByRole('checkbox', { name: /jit provisioning/i }));
    await user.click(screen.getByRole('button', { name: /save provider/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/providers/provider-1`,
        expect.objectContaining({
          method: 'PATCH',
          body: expect.stringContaining('"displayName":"Google Workspace"'),
        })
      );
    });

    expect(await screen.findByRole('heading', { name: /google workspace/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /delete provider/i }));
    await user.click(within(await screen.findByRole('dialog', { name: /delete provider/i })).getByRole('button', { name: /delete google/i }));

    await waitFor(() => {
      const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
      expect(calls).toContain(`DELETE ${apiBase}/api/admin/providers/provider-1`);
    });
  }, 10000);

  it('validates provider create input before submitting', async () => {
    const user = userEvent.setup();
    const fetchMock = setupProviderFetch();

    renderManagementWeb(
      <ProvidersPage permissions={['providers:read', 'providers:write']} />,
      { initialEntries: ['/providers/new'] }
    );

    expect(await screen.findByRole('heading', { name: /create provider/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /create provider/i }));

    expect(await screen.findByText(/provider name is required/i)).toBeInTheDocument();
    expect(screen.getByText(/display name is required/i)).toBeInTheDocument();
    expect(screen.getByText(/authority is required/i)).toBeInTheDocument();
    expect(screen.getByText(/client id is required/i)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalledWith(
      `${apiBase}/api/admin/providers`,
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('hides write actions when write and delete permissions are missing', async () => {
    setupProviderFetch();

    renderManagementWeb(<ProvidersPage permissions={['providers:read']} />);

    expect(await screen.findByText(/read-only access/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /new provider/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete google/i })).not.toBeInTheDocument();
  });

  it('opens authentication settings from the identity providers context', async () => {
    const user = userEvent.setup();
    setupProviderFetch();

    renderManagementWeb(
      <ProvidersPage permissions={['providers:read', 'system:settings']} />,
      { initialEntries: ['/providers'] }
    );

    expect(await screen.findByRole('heading', { name: /^identity providers$/i })).toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: /authentication settings/i }));

    expect(await screen.findByRole('heading', { name: /authentication settings/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /default authentication provider/i })).toBeInTheDocument();
  });

  it('hides authentication settings link when the operator lacks settings permission', async () => {
    setupProviderFetch();

    renderManagementWeb(<ProvidersPage permissions={['providers:read']} />);

    expect(await screen.findByRole('heading', { name: /^identity providers$/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /authentication settings/i })).not.toBeInTheDocument();
  });
});
