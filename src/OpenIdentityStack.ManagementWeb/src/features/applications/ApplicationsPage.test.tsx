import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { ApplicationsPage } from './ApplicationsPage';

const apiBase = 'http://localhost:5000';
const originalFetch = globalThis.fetch;

function jsonResponse(body: unknown) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  }));
}

afterEach(() => {
  vi.useRealTimers();
  globalThis.fetch = originalFetch;
});

describe('ApplicationsPage', () => {
  it('lists applications and filters through the consolidated applications endpoint', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/applications?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [{
            id: 'app-1',
            clientId: 'orders-web',
            displayName: 'Orders Web',
            profile: 'Web',
            clientType: 'Confidential',
            status: 'Active',
            allowedGrantTypes: ['authorization_code'],
            credentialCount: 1,
            createdAt: '2026-01-01T00:00:00Z',
            modifiedAt: null,
          }],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/applications?page=1&pageSize=20&search=worker` && method === 'GET') {
        return jsonResponse({
          items: [{
            id: 'app-2',
            clientId: 'worker',
            displayName: 'Worker',
            profile: 'MachineToMachine',
            clientType: 'Confidential',
            status: 'Disabled',
            allowedGrantTypes: ['client_credentials'],
            credentialCount: 0,
            createdAt: '2026-01-02T00:00:00Z',
            modifiedAt: null,
          }],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<ApplicationsPage />);

    expect(await screen.findByRole('cell', { name: 'orders-web' })).toBeInTheDocument();

    await user.type(screen.getByPlaceholderText(/search by client id or display name/i), 'worker');

    expect(await screen.findByRole('cell', { name: 'worker' })).toBeInTheDocument();
    expect(screen.queryByRole('cell', { name: 'orders-web' })).not.toBeInTheDocument();

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/applications?page=1&pageSize=20&search=worker`,
        expect.anything()
      );
    });
  });

  it('applies profile, status, and client type filters through the consolidated endpoint', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/applications?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [],
          totalCount: 0,
          page: 1,
          pageSize: 20,
          totalPages: 0,
        });
      }

      if (
        url ===
          `${apiBase}/api/admin/applications?page=1&pageSize=20&profile=MachineToMachine&status=Disabled&clientType=Confidential` &&
        method === 'GET'
      ) {
        return jsonResponse({
          items: [{
            id: 'app-2',
            clientId: 'worker',
            displayName: 'Worker',
            profile: 'MachineToMachine',
            clientType: 'Confidential',
            status: 'Disabled',
            allowedGrantTypes: ['client_credentials'],
            credentialCount: 0,
            createdAt: '2026-01-02T00:00:00Z',
            modifiedAt: null,
          }],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<ApplicationsPage />);

    await screen.findByText('No applications found');
    await user.selectOptions(screen.getByLabelText(/profile/i), 'MachineToMachine');
    await user.selectOptions(screen.getByLabelText(/status/i), 'Disabled');
    await user.selectOptions(screen.getByLabelText(/client type/i), 'Confidential');

    expect(await screen.findByRole('cell', { name: 'worker' })).toBeInTheDocument();
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/applications?page=1&pageSize=20&profile=MachineToMachine&status=Disabled&clientType=Confidential`,
        expect.anything()
      );
    });
  });

  it('confirms deletion through the consolidated applications endpoint', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/applications?page=1&pageSize=20` && method === 'GET') {
        return jsonResponse({
          items: [{
            id: 'app-1',
            clientId: 'orders-web',
            displayName: 'Orders Web',
            profile: 'Web',
            clientType: 'Confidential',
            status: 'Active',
            allowedGrantTypes: ['authorization_code'],
            credentialCount: 1,
            createdAt: '2026-01-01T00:00:00Z',
            modifiedAt: null,
          }],
          totalCount: 1,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        });
      }

      if (url === `${apiBase}/api/admin/applications/app-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(<ApplicationsPage />);

    await screen.findByRole('cell', { name: 'orders-web' });
    await user.click(screen.getByRole('button', { name: /delete orders web/i }));
    await user.click(await screen.findByRole('button', { name: /^delete$/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/applications/app-1`,
        expect.objectContaining({ method: 'DELETE' })
      );
    });
  });
});
