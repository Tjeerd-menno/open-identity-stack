import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderManagementWeb } from '@/test/render';
import { AuditEntriesPage } from './AuditEntriesPage';

const apiBase = 'http://localhost:5000';

const auditEntry = {
  id: 'audit-1',
  timestamp: '2026-06-01T12:00:00Z',
  userId: 'admin-user',
  action: 'Application.Updated',
  entityType: 'Application',
  entityId: 'orders-web',
  details: 'Changed redirect URL',
  beforeState: '{"redirect":"old"}',
  afterState: '{"redirect":"new"}',
};

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

function auditEntriesResponse() {
  return {
    items: [auditEntry],
    totalCount: 21,
    page: 1,
    pageSize: 20,
    totalPages: 2,
    hasPreviousPage: false,
    hasNextPage: true,
  };
}

describe('AuditEntriesPage', () => {
  it('lists, filters, paginates, and expands audit entry details', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = input.toString();

      if (url === `${apiBase}/api/admin/audit-entries?page=1&pageSize=20`) {
        return jsonResponse(auditEntriesResponse());
      }

      if (url.includes('/api/admin/audit-entries?page=1&pageSize=20')
        && url.includes('userId=admin-user')
        && url.includes('action=Application.Updated')
        && url.includes('entityType=Application')
        && url.includes('entityId=orders-web')
        && url.includes('search=redirect')) {
        return jsonResponse(auditEntriesResponse());
      }

      if (url.includes('/api/admin/audit-entries?page=2&pageSize=20')) {
        return jsonResponse({ ...auditEntriesResponse(), page: 2 });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    renderManagementWeb(
      <AuditEntriesPage permissions={['audit-logs:read']} />,
      { initialEntries: ['/audit-entries'] }
    );

    expect(await screen.findByRole('heading', { name: /^audit$/i })).toBeInTheDocument();
    expect(await screen.findByText('Application.Updated')).toBeInTheDocument();
    expect(screen.getByText('orders-web')).toBeInTheDocument();

    await user.type(screen.getByLabelText(/user id/i), 'admin-user');
    await user.type(screen.getByLabelText(/^action$/i), 'Application.Updated');
    await user.type(screen.getByLabelText(/entity type/i), 'Application');
    await user.type(screen.getByLabelText(/entity id/i), 'orders-web');
    await user.type(screen.getByLabelText(/search audit entries/i), 'redirect');
    await user.click(screen.getByRole('button', { name: /apply filters/i }));

    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url]) => url.toString().includes('search=redirect'))).toBe(true);
    });

    await user.click(screen.getByRole('button', { name: /expand audit entry/i }));
    const details = await screen.findByRole('region', { name: /audit entry details/i });
    expect(within(details).getByText(/Changed redirect URL/)).toBeInTheDocument();
    expect(within(details).getByText('{"redirect":"old"}')).toBeInTheDocument();
    expect(within(details).getByText('{"redirect":"new"}')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /next/i }));

    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url]) => url.toString().includes('page=2&pageSize=20'))).toBe(true);
    });
  });

  it('shows access denied when audit read permission is missing', async () => {
    renderManagementWeb(<AuditEntriesPage permissions={[]} />);

    expect(screen.getByRole('heading', { name: /access denied/i })).toBeInTheDocument();
    expect(screen.getByText(/audit-logs:read/i)).toBeInTheDocument();
  });
});
