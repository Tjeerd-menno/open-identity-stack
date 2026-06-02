import { getAuditEntries } from './audit-entries-api';

const apiBase = 'http://localhost:5000';

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

describe('audit-entries-api', () => {
  it('uses the admin audit entries endpoint with pagination and filters', async () => {
    const fetchMock = vi.fn(() => jsonResponse({
      items: [],
      totalCount: 0,
      page: 2,
      pageSize: 25,
      totalPages: 0,
      hasPreviousPage: true,
      hasNextPage: false,
    }));
    vi.stubGlobal('fetch', fetchMock);

    await getAuditEntries({
      page: 2,
      pageSize: 25,
      from: '2026-06-01T00:00:00Z',
      to: '2026-06-02T00:00:00Z',
      userId: 'admin-user',
      action: 'Application.Updated',
      entityType: 'Application',
      entityId: 'orders-web',
      search: 'redirect',
    });

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/audit-entries?page=2&pageSize=25&from=2026-06-01T00%3A00%3A00Z&to=2026-06-02T00%3A00%3A00Z&userId=admin-user&action=Application.Updated&entityType=Application&entityId=orders-web&search=redirect`,
      expect.anything()
    );
  });
});
