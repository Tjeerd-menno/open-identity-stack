import {
  addApplicationPermission,
  addDelegatedMaintainer,
  applyPermissionManifest,
  applyRemotePermissionManifest,
  changeApplicationLifecycle,
  getApplicationPermissionDiagnostics,
  getApplicationPermissionHistory,
  getAssignablePermissionCatalog,
  getRegisteredApplication,
  getRegisteredApplications,
  importPermissionManifestFromEndpoint,
  previewPermissionManifest,
  previewRemotePermissionManifest,
  registerPermissionManifest,
  removeDelegatedMaintainer,
  transferApplicationOwnership,
  updateRegisteredApplication,
  updateRemovedPermissionReplacement,
} from './application-permissions-api';
import type { PermissionManifestRequest } from './application-permissions-api';

const apiBase = 'http://localhost:5000';

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  }));
}

const manifestRequest: PermissionManifestRequest = {
  manifest: {
    schemaVersion: '1.0.0',
    application: {
      id: 'patient-api',
      displayName: 'Patient API',
      description: 'Patient records',
      version: '1.0.0',
    },
    permissions: [
      {
        key: 'patient:read',
        displayName: 'Read patients',
        description: 'Allows reading patient data',
        category: 'Patients',
      },
    ],
  },
  ownerId: 'owner-1',
  ownerType: 'user' as const,
  manifestBaseUrl: null,
};

describe('application-permissions-api', () => {
  it('maps application permission registry calls to the backend contract', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      const method = init?.method ?? 'GET';

      if (url.startsWith(`${apiBase}/api/admin/application-permissions/applications`) && method === 'GET') {
        if (url.includes('/application-1')) {
          return jsonResponse(registeredApplication());
        }

        return jsonResponse({ items: [registeredApplicationListItem()], totalCount: 1, page: 2, pageSize: 10, totalPages: 1 });
      }

      if (url === `${apiBase}/api/admin/application-permissions/applications` && method === 'POST') {
        return jsonResponse({ applicationId: 'application-1' });
      }

      if (url === `${apiBase}/api/admin/application-permissions/applications/import` && method === 'POST') {
        return jsonResponse({ applicationId: 'application-1' });
      }

      if (url.includes('/manifest/preview') && method === 'POST') {
        return jsonResponse({ applicationId: 'application-1', removals: [] });
      }

      if (url.endsWith('/manifest') && method === 'POST') {
        return jsonResponse({ application: registeredApplication(), result: { removedPermissions: [] } });
      }

      if (url.includes('/import/preview') && method === 'POST') {
        return jsonResponse({ applicationId: 'application-1', removals: [] });
      }

      if (url.endsWith('/import') && method === 'POST') {
        return jsonResponse({ application: registeredApplication(), result: { removedPermissions: [] } });
      }

      if (url.endsWith('/applications/application-1') && method === 'PATCH') {
        return jsonResponse(registeredApplication());
      }

      if (url.endsWith('/applications/application-1/permissions') && method === 'POST') {
        return jsonResponse(registeredApplication());
      }

      if (url.endsWith('/applications/application-1/lifecycle') && method === 'POST') {
        return jsonResponse(registeredApplication());
      }

      if (url.endsWith('/applications/application-1/ownership') && method === 'POST') {
        return jsonResponse(registeredApplication());
      }

      if (url.endsWith('/applications/application-1/maintainers') && method === 'POST') {
        return jsonResponse(registeredApplication());
      }

      if (url.endsWith('/maintainers/owner%40example.com?concurrencyToken=7') && method === 'DELETE') {
        return jsonResponse(registeredApplication());
      }

      if (url.startsWith(`${apiBase}/api/admin/application-permissions/catalog`) && method === 'GET') {
        return jsonResponse({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 });
      }

      if (url.startsWith(`${apiBase}/api/admin/application-permissions/history`) && method === 'GET') {
        return jsonResponse({ deletedApplications: [], removedPermissions: [] });
      }

      if (url === `${apiBase}/api/admin/application-permissions/diagnostics` && method === 'GET') {
        return jsonResponse({ issues: [] });
      }

      if (url.endsWith('/permissions/permission-1/replacement') && method === 'PATCH') {
        return jsonResponse({ id: 'permission-1' });
      }

      return Promise.resolve(new Response(null, { status: 404 }));
    });
    vi.stubGlobal('fetch', fetchMock);

    await getRegisteredApplications({ page: 2, pageSize: 10, search: 'patient' });
    await getRegisteredApplication('application-1');
    await registerPermissionManifest(manifestRequest);
    await importPermissionManifestFromEndpoint('https://patient.example/.well-known/permissions');
    await previewPermissionManifest('application-1', manifestRequest, 3);
    await applyPermissionManifest('application-1', manifestRequest, 4);
    await previewRemotePermissionManifest('application-1', 5);
    await applyRemotePermissionManifest('application-1', 6);
    await updateRegisteredApplication('application-1', {
      displayName: 'Patient API',
      description: 'Patient records',
      manifestBaseUrl: null,
      concurrencyToken: 7,
    });
    await addApplicationPermission('application-1', {
      permissionKey: 'patient:write',
      displayName: 'Write patients',
      description: 'Allows writing patient data',
      category: 'Patients',
      concurrencyToken: 7,
    });
    await changeApplicationLifecycle('application-1', { status: 'Disabled', acknowledgeDependencies: true, concurrencyToken: 7 });
    await transferApplicationOwnership('application-1', { ownerId: 'owner-2', ownerType: 'Group', concurrencyToken: 7 });
    await addDelegatedMaintainer('application-1', { principalId: 'owner@example.com', principalType: 'User', concurrencyToken: 7 });
    await removeDelegatedMaintainer('application-1', 'owner@example.com', 7);
    await getAssignablePermissionCatalog({ page: 1, pageSize: 100 });
    await getApplicationPermissionHistory({ applicationIdentifier: 'patient-api' });
    await getApplicationPermissionDiagnostics();
    await updateRemovedPermissionReplacement('permission-1', {
      replacementFullPermissionKey: 'users:read',
      replacementNote: 'Use platform permission.',
      concurrencyToken: 8,
    });

    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/application-permissions/applications?page=2&pageSize=10&search=patient`,
      expect.any(Object)
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/application-permissions/applications`,
      expect.objectContaining({ method: 'POST', body: JSON.stringify(manifestRequest) })
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/application-permissions/applications/import`,
      expect.objectContaining({ method: 'POST', body: '{"endpoint":"https://patient.example/.well-known/permissions"}' })
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/application-permissions/applications/application-1/manifest/preview`,
      expect.objectContaining({ method: 'POST', body: JSON.stringify({ manifest: manifestRequest.manifest, concurrencyToken: 3 }) })
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/application-permissions/applications/application-1/maintainers/owner%40example.com?concurrencyToken=7`,
      expect.objectContaining({ method: 'DELETE' })
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/application-permissions/catalog?page=1&pageSize=100`,
      expect.any(Object)
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/application-permissions/history?applicationIdentifier=patient-api`,
      expect.any(Object)
    );
    expect(fetchMock).toHaveBeenCalledWith(
      `${apiBase}/api/admin/application-permissions/diagnostics`,
      expect.any(Object)
    );
  });
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
