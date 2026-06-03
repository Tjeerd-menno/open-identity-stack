import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Route, Routes } from 'react-router';
import { renderManagementWeb } from '@/test/render';
import { ApplicationDetailPage } from './ApplicationDetailPage';

const apiBase = 'http://localhost:5000';
const originalFetch = globalThis.fetch;

function renderDetailPage() {
  renderManagementWeb(
    <Routes>
      <Route path="/applications" element={<div>Applications list</div>} />
      <Route path="/applications/:id" element={<ApplicationDetailPage />} />
    </Routes>,
    { initialEntries: ['/applications/app-1'] }
  );
}

function jsonResponse(body: unknown) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  }));
}

function application(overrides: Record<string, unknown> = {}) {
  return {
    id: 'app-1',
    clientId: 'orders-web',
    displayName: 'Orders Web',
    description: 'Orders UI',
    profile: 'Web',
    clientType: 'Confidential',
    status: 'Active',
    redirectUris: ['https://orders.example.com/callback'],
    postLogoutRedirectUris: ['https://orders.example.com/'],
    allowedScopes: ['openid', 'orders.read'],
    allowedGrantTypes: ['authorization_code'],
    requirePkce: true,
    requireConsent: true,
    credentialCount: 1,
    certificateCount: 0,
    requiresMigrationReview: false,
    migrationSource: null,
    createdAt: '2026-01-01T00:00:00Z',
    modifiedAt: null,
    ...overrides,
  };
}

afterEach(() => {
  globalThis.fetch = originalFetch;
});

describe('ApplicationDetailPage', () => {
  it('renders application details and performs lifecycle/delete actions through consolidated endpoints', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/applications/app-1` && method === 'GET') {
        return jsonResponse(application());
      }

      if (url === `${apiBase}/api/admin/applications/app-1/credentials` && method === 'GET') {
        return jsonResponse([]);
      }

      if (url === `${apiBase}/api/admin/applications/app-1/disable` && method === 'POST') {
        return jsonResponse(application({ status: 'Disabled' }));
      }

      if (url === `${apiBase}/api/admin/applications/app-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderDetailPage();

    expect(await screen.findByRole('heading', { name: 'Orders Web' })).toBeInTheDocument();
    expect(screen.getByText('orders-web')).toBeInTheDocument();
    expect(screen.getByText('https://orders.example.com/callback')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^disable$/i }));
    await user.click(screen.getByRole('button', { name: /^delete$/i }));
    await user.click(await screen.findByRole('button', { name: /^delete application$/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/applications/app-1/disable`,
        expect.objectContaining({ method: 'POST' })
      );
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/applications/app-1`,
        expect.objectContaining({ method: 'DELETE' })
      );
    });
  });

  it('blocks credentials for public applications', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => {
      if (input.toString() === `${apiBase}/api/admin/applications/app-1`) {
        return jsonResponse(application({ clientType: 'Public', profile: 'SinglePage' }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }));

    renderDetailPage();

    expect(await screen.findByText(/public applications cannot use client secrets or certificates/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add secret/i })).not.toBeInTheDocument();
  });

  it('adds and revokes confidential application credentials', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/applications/app-1` && method === 'GET') {
        return jsonResponse(application());
      }

      if (url === `${apiBase}/api/admin/applications/app-1/credentials` && method === 'GET') {
        return jsonResponse([{
          id: 'cred-1',
          applicationId: 'app-1',
          type: 'ClientSecret',
          thumbprint: null,
          subject: null,
          description: 'Primary secret',
          expiresAt: null,
          createdAt: '2026-01-02T00:00:00Z',
          lastUsedAt: null,
          revokedAt: null,
        }]);
      }

      if (url === `${apiBase}/api/admin/applications/app-1/credentials/client-secrets` && method === 'POST') {
        return jsonResponse({ credentialId: 'cred-2', clientSecret: 'new-secret' });
      }

      if (url === `${apiBase}/api/admin/applications/app-1/credentials/cred-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderDetailPage();

    expect(await screen.findByText('Primary secret')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /add secret/i }));
    await user.type(await screen.findByLabelText(/description/i), 'Rotated secret');
    await user.click(screen.getByLabelText(/revoke existing secrets/i));
    await user.click(within(screen.getByRole('dialog', { name: /add secret/i })).getByRole('button', { name: /^add secret$/i }));

    expect(await screen.findByText('••••••••••••••••')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /show client secret/i }));
    expect(screen.getByText('new-secret')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /revoke primary secret/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/applications/app-1/credentials/client-secrets`,
        expect.objectContaining({
          method: 'POST',
          body: JSON.stringify({
            description: 'Rotated secret',
            expiresAt: null,
            revokeExisting: true,
          }),
        })
      );
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/applications/app-1/credentials/cred-1`,
        expect.objectContaining({ method: 'DELETE' })
      );
    });
  });
});
