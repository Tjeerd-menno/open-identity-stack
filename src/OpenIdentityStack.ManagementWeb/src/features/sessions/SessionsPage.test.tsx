import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderManagementWeb } from '@/test/render';
import { SessionsPage } from './SessionsPage';

const apiBase = 'http://localhost:5000';

const activeSession = {
  id: 'session-1',
  userId: 'user-1',
  ipAddress: '10.0.0.5',
  userAgent: 'Firefox on Windows',
  status: 'Active',
  clientCount: 2,
  lastActivityAt: '2026-01-02T10:00:00Z',
  expiresAt: '2026-01-02T12:00:00Z',
  createdAt: '2026-01-02T08:00:00Z',
};

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

function sessionsResponse() {
  return {
    items: [activeSession],
    totalCount: 1,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  };
}

describe('SessionsPage', () => {
  it('lists, searches, filters, views, and revokes sessions', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/sessions?page=1&pageSize=20&status=Active` && method === 'GET') {
        return jsonResponse(sessionsResponse());
      }

      if (url === `${apiBase}/api/admin/sessions?page=1&pageSize=20&search=10.0.0.5&status=Active` && method === 'GET') {
        return jsonResponse(sessionsResponse());
      }

      if (url === `${apiBase}/api/admin/sessions?page=1&pageSize=20&search=10.0.0.5&status=Revoked` && method === 'GET') {
        return jsonResponse({ ...sessionsResponse(), items: [{ ...activeSession, status: 'Revoked' }] });
      }

      if (url === `${apiBase}/api/admin/sessions/session-1` && method === 'GET') {
        return jsonResponse(activeSession);
      }

      if (url === `${apiBase}/api/admin/sessions/session-1` && method === 'DELETE') {
        return Promise.resolve(new Response(null, { status: 204 }));
      }

      if (url === `${apiBase}/api/admin/users/user-1/sessions` && method === 'DELETE') {
        return jsonResponse({ revokedCount: 2, revokedAt: '2026-01-02T11:00:00Z' });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    const { unmount } = renderManagementWeb(
      <SessionsPage permissions={['sessions:read', 'sessions:revoke']} />,
      { initialEntries: ['/sessions'] }
    );

    expect(await screen.findByRole('heading', { name: /^sessions$/i })).toBeInTheDocument();
    expect(await screen.findByText('10.0.0.5')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /applications/i })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: /clients/i })).not.toBeInTheDocument();

    await user.type(screen.getByLabelText(/search sessions/i), '10.0.0.5');
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/sessions?page=1&pageSize=20&search=10.0.0.5&status=Active`,
        expect.anything()
      );
    });

    await user.selectOptions(screen.getByLabelText(/filter by status/i), 'Revoked');
    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/sessions?page=1&pageSize=20&search=10.0.0.5&status=Revoked`,
        expect.anything()
      );
    });

    await user.click(screen.getByRole('button', { name: /view session session-1/i }));
    expect(await screen.findByRole('heading', { name: /session details/i })).toBeInTheDocument();
    expect(screen.getByText(/Firefox on Windows/)).toBeInTheDocument();
    expect(screen.getByText(/application count: 2/i)).toBeInTheDocument();
    expect(screen.queryByText(/client count/i)).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^revoke session$/i }));
    await user.click(within(await screen.findByRole('dialog', { name: /revoke session/i })).getByRole('button', { name: /revoke session/i }));

    await waitFor(() => {
      const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
      expect(calls).toContain(`DELETE ${apiBase}/api/admin/sessions/session-1`);
    });

    unmount();

    renderManagementWeb(
      <SessionsPage permissions={['sessions:read', 'sessions:revoke']} />,
      { initialEntries: ['/sessions/session-1'] }
    );

    await screen.findByRole('heading', { name: /session details/i });
    await user.click(screen.getByRole('button', { name: /revoke all sessions for user/i }));
    await user.click(within(await screen.findByRole('dialog', { name: /revoke all user sessions/i })).getByRole('button', { name: /revoke all user sessions/i }));

    await waitFor(() => {
      const calls = fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? 'GET'} ${url.toString()}`);
      expect(calls).toContain(`DELETE ${apiBase}/api/admin/users/user-1/sessions`);
    });
  });

  it('hides revoke actions when revoke permission is missing', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url === `${apiBase}/api/admin/sessions?page=1&pageSize=20&status=Active` && method === 'GET') {
        return jsonResponse(sessionsResponse());
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    }));

    renderManagementWeb(<SessionsPage permissions={['sessions:read']} />);

    expect(await screen.findByText(/read-only access/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /revoke session/i })).not.toBeInTheDocument();
  });
});
