import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderManagementWeb } from '@/test/render';
import { ApplicationPermissionsPage } from './ApplicationPermissionsPage';

const apiBase = 'http://localhost:5000';

let application = registeredApplication();

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

function setupApplicationPermissionsFetch() {
  application = registeredApplication();

  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const url = input.toString();
    const method = init?.method ?? 'GET';

    if (url.startsWith(`${apiBase}/api/admin/application-permissions/applications?page=`) && method === 'GET') {
      return jsonResponse({ items: [registeredApplicationListItem()], totalCount: 1, page: 1, pageSize: 20, totalPages: 1 });
    }

    if (url === `${apiBase}/api/admin/application-permissions/applications/application-1` && method === 'GET') {
      return jsonResponse(application);
    }

    if (url === `${apiBase}/api/admin/application-permissions/applications` && method === 'POST') {
      return jsonResponse({ applicationId: 'application-2' });
    }

    if (url === `${apiBase}/api/admin/application-permissions/applications/import` && method === 'POST') {
      return jsonResponse({ applicationId: 'imported-application' });
    }

    if (url === `${apiBase}/api/admin/application-permissions/applications/application-1/lifecycle` && method === 'POST') {
      application = { ...application, status: 'Disabled' };
      return jsonResponse(application);
    }

    if (url === `${apiBase}/api/admin/application-permissions/applications/application-1/permissions` && method === 'POST') {
      application = {
        ...application,
        permissions: [
          ...application.permissions,
          {
            id: 'permission-2',
            key: 'patient:write',
            fullPermissionKey: 'patient:write',
            displayName: 'patient:write',
            description: 'Allows writing patient data',
            category: 'Patients',
            status: 'Active',
          },
        ],
      };
      return jsonResponse(application);
    }

    if (url === `${apiBase}/api/admin/application-permissions/applications/application-1/ownership` && method === 'POST') {
      application = { ...application, ownerId: 'group-1', ownerType: 'Group' };
      return jsonResponse(application);
    }

    if (url === `${apiBase}/api/admin/application-permissions/applications/application-1/maintainers` && method === 'POST') {
      application = {
        ...application,
        maintainers: [...application.maintainers, { id: 'maintainer-2', principalId: 'user-2', principalType: 'User' }],
      };
      return jsonResponse(application);
    }

    if (url === `${apiBase}/api/admin/application-permissions/applications/application-1/maintainers/user-2?concurrencyToken=7` && method === 'DELETE') {
      application = { ...application, maintainers: application.maintainers.filter((maintainer) => maintainer.principalId !== 'user-2') };
      return jsonResponse(application);
    }

    if (url === `${apiBase}/api/admin/application-permissions/catalog?page=1&pageSize=50` && method === 'GET') {
      return jsonResponse({ items: [{ fullPermissionKey: 'patient:read', displayName: 'Read patients', applicationIdentifier: 'patient-api' }], totalCount: 1, page: 1, pageSize: 50, totalPages: 1 });
    }

    if (url === `${apiBase}/api/admin/application-permissions/history?applicationIdentifier=patient-api` && method === 'GET') {
      return jsonResponse({ deletedApplications: [], removedPermissions: [{ fullPermissionKey: 'patient:legacy', removedAt: '2026-01-01T00:00:00Z' }] });
    }

    if (url === `${apiBase}/api/admin/application-permissions/diagnostics` && method === 'GET') {
      return jsonResponse({ issues: [{ fullPermissionKey: 'patient:legacy', roleName: 'Legacy Role', issueType: 'MissingPermission' }] });
    }

    return Promise.resolve(new Response(null, { status: 404 }));
  });

  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('ApplicationPermissionsPage', () => {
  it('lists registered applications and links to register and detail routes', async () => {
    setupApplicationPermissionsFetch();

    renderManagementWeb(<ApplicationPermissionsPage permissions={['*']} />, { initialEntries: ['/application-permissions'] });

    expect(await screen.findByRole('heading', { name: 'Permissions' })).toBeInTheDocument();
    expect(await screen.findByText('Patient API')).toBeInTheDocument();
    expect(screen.getByText('patient-api')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add application/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /view patient api/i })).toBeInTheDocument();
  });

  it('registers manual manifests and imports well-known permission endpoints', async () => {
    const user = userEvent.setup();
    const fetchMock = setupApplicationPermissionsFetch();

    cleanup();
    renderManagementWeb(<ApplicationPermissionsPage permissions={['*']} />, { initialEntries: ['/application-permissions/new'] });

    expect(await screen.findByRole('heading', { name: /add application/i })).toBeInTheDocument();

    await user.type(screen.getByLabelText(/well-known permissions endpoint/i), 'https://patient.example/.well-known/permissions');
    await user.click(screen.getByRole('button', { name: /import endpoint/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/application-permissions/applications/import`,
        expect.objectContaining({ method: 'POST', body: '{"endpoint":"https://patient.example/.well-known/permissions"}' })
      );
    });

    renderManagementWeb(<ApplicationPermissionsPage permissions={['*']} />, { initialEntries: ['/application-permissions/new'] });
    expect(await screen.findByRole('heading', { name: /add application/i })).toBeInTheDocument();

    await user.type(screen.getByLabelText(/application id/i), 'patient-api');
    await user.type(screen.getByLabelText(/^display name$/i), 'Patient API');
    await user.type(screen.getByLabelText(/^description$/i), 'Patient records');
    await user.type(screen.getByLabelText(/^version$/i), '1.0.0');
    await user.type(screen.getByLabelText(/^owner id$/i), 'owner-1');
    await user.type(screen.getByLabelText(/^permission key 1$/i), 'patient:read');
    await user.type(screen.getByLabelText(/^permission display name 1$/i), 'Read patients');
    await user.type(screen.getByLabelText(/^permission description 1$/i), 'Allows reading patient data');
    await user.type(screen.getByLabelText(/^permission category 1$/i), 'Patients');
    await user.click(screen.getByRole('button', { name: /^add application$/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/application-permissions/applications`,
        expect.objectContaining({ method: 'POST' })
      );
    });
  }, 10000);

  it('shows detail ownership, maintainers, permissions, catalog, history, and diagnostics controls', async () => {
    const user = userEvent.setup();
    const fetchMock = setupApplicationPermissionsFetch();

    renderManagementWeb(<ApplicationPermissionsPage permissions={['*']} />, { initialEntries: ['/application-permissions/application-1'] });

    expect(await screen.findByRole('heading', { name: 'Patient API' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Ownership' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Maintainers' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Permissions' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Catalog' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'History' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Diagnostics' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Edit' }));
    await user.click(screen.getByRole('button', { name: /disable/i }));
    await user.type(screen.getByLabelText(/new owner id/i), 'group-1');
    await user.selectOptions(screen.getByLabelText(/new owner type/i), 'Group');
    await user.click(screen.getByRole('button', { name: /transfer ownership/i }));
    await user.type(screen.getByLabelText(/new maintainer id/i), 'user-2');
    await user.selectOptions(screen.getByLabelText(/new maintainer type/i), 'User');
    await user.click(screen.getByRole('button', { name: /add maintainer/i }));
    await user.type(screen.getByLabelText(/permission name/i), 'patient:write');
    await user.type(screen.getByLabelText(/permission category/i), 'Patients');
    await user.type(screen.getByLabelText(/permission description/i), 'Allows writing patient data');
    await user.click(screen.getByRole('button', { name: /^add permission$/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/application-permissions/applications/application-1/lifecycle`,
        expect.objectContaining({ method: 'POST' })
      );
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/application-permissions/applications/application-1/ownership`,
        expect.objectContaining({ method: 'POST', body: '{"ownerId":"group-1","ownerType":"Group","concurrencyToken":7}' })
      );
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/application-permissions/applications/application-1/maintainers`,
        expect.objectContaining({ method: 'POST', body: '{"principalId":"user-2","principalType":"User","concurrencyToken":7}' })
      );
      expect(fetchMock).toHaveBeenCalledWith(
        `${apiBase}/api/admin/application-permissions/applications/application-1/permissions`,
        expect.objectContaining({ method: 'POST' })
      );
    });

    expect(await screen.findByText('patient:write')).toBeInTheDocument();
    expect(screen.getAllByText('patient:legacy').length).toBeGreaterThan(0);
    expect(screen.getByText('Legacy Role')).toBeInTheDocument();
  }, 10000);
});

function registeredApplicationListItem() {
  return {
    id: 'application-1',
    applicationIdentifier: 'patient-api',
    displayName: 'Patient API',
    ownerId: 'owner-1',
    ownerType: 'User',
    permissionCount: 1,
    status: 'Active',
    manifestVersion: '1.0.0',
    updatedAt: '2026-01-01T00:00:00Z',
  };
}

function registeredApplication() {
  return {
    ...registeredApplicationListItem(),
    description: 'Patient records',
    schemaVersion: '1.0.0',
    manifestBaseUrl: null,
    concurrencyToken: 7,
    permissions: [
      {
        id: 'permission-1',
        key: 'patient:read',
        fullPermissionKey: 'patient:read',
        displayName: 'Read patients',
        description: 'Allows reading patient data',
        category: 'Patients',
        status: 'Active',
      },
    ],
    maintainers: [
      {
        id: 'maintainer-1',
        principalId: 'owner@example.com',
        principalType: 'User',
      },
    ],
  };
}
