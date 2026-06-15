import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderManagementWeb } from '@/test/render';
import { SettingsPage } from './SettingsPage';

const apiBase = 'http://localhost:5000';

let settings = {
  defaultProviderId: 'local',
  isLocalDefault: true,
  localFallbackEnabled: true,
  updatedAt: '2026-01-01T00:00:00Z',
};

const providers = [
  { id: 'local', name: 'local', displayName: 'Local Accounts', type: 'local', isActive: true },
  { id: 'provider-1', name: 'google', displayName: 'Google Login', type: 'external', isActive: true },
  { id: 'provider-2', name: 'disabled', displayName: 'Disabled Provider', type: 'external', isActive: false },
];

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

function setupSettingsFetch() {
  settings = {
    defaultProviderId: 'local',
    isLocalDefault: true,
    localFallbackEnabled: true,
    updatedAt: '2026-01-01T00:00:00Z',
  };

  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = input.toString();
    const method = init?.method ?? 'GET';

    if (url === `${apiBase}/api/admin/authentication-settings` && method === 'GET') {
      return jsonResponse(settings);
    }

    if (url === `${apiBase}/api/admin/authentication-settings/providers` && method === 'GET') {
      return jsonResponse(providers);
    }

    if (url === `${apiBase}/api/admin/authentication-settings/default-provider` && method === 'PUT') {
      const body = JSON.parse(init?.body?.toString() ?? '{}') as { providerId: string };
      settings = {
        ...settings,
        defaultProviderId: body.providerId,
        isLocalDefault: body.providerId === 'local',
        updatedAt: '2026-01-02T00:00:00Z',
      };
      return jsonResponse(settings);
    }

    if (url === `${apiBase}/api/admin/authentication-settings/local-fallback` && method === 'PUT') {
      const body = JSON.parse(init?.body?.toString() ?? '{}') as { enabled: boolean };
      settings = {
        ...settings,
        localFallbackEnabled: body.enabled,
        updatedAt: '2026-01-03T00:00:00Z',
      };
      return jsonResponse(settings);
    }

    return Promise.resolve(new Response(null, { status: 404 }));
  });

  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('SettingsPage', () => {
  it('shows authentication settings and updates default provider and local fallback', async () => {
    const user = userEvent.setup();
    const fetchMock = setupSettingsFetch();

    renderManagementWeb(<SettingsPage />, { initialEntries: ['/settings'] });

    expect(await screen.findByRole('heading', { name: /authentication settings/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /default authentication provider/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /admin local fallback/i })).toBeInTheDocument();
    expect(screen.getByText(/local accounts \(default\)/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/enable local fallback for iam admins/i)).toBeDisabled();

    await user.selectOptions(screen.getByLabelText(/default provider/i), 'provider-1');

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/authentication-settings/default-provider`,
        expect.objectContaining({ method: 'PUT', body: '{"providerId":"provider-1"}' })
      );
    });

    expect(await screen.findByText(/default authentication provider updated successfully/i)).toBeInTheDocument();
    expect(screen.getAllByText(/external provider/i).length).toBeGreaterThan(0);
    expect(screen.getByLabelText(/enable local fallback for iam admins/i)).not.toBeDisabled();

    await user.click(screen.getByLabelText(/enable local fallback for iam admins/i));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/authentication-settings/local-fallback`,
        expect.objectContaining({ method: 'PUT', body: '{"enabled":false}' })
      );
    });

    expect(await screen.findByText(/local fallback setting updated successfully/i)).toBeInTheDocument();
  });

  it('renders a load error when settings or providers fail', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(new Response(JSON.stringify({ error: 'Nope' }), {
      status: 500,
      headers: { 'Content-Type': 'application/json' },
    }))));

    renderManagementWeb(<SettingsPage />);

    expect(await screen.findByText(/failed to load authentication settings/i)).toBeInTheDocument();
  });
});
