import { describe, expect, it, vi, beforeEach } from 'vitest';
import { apiClient } from '@/lib/api/client';
import {
  getAssignablePermissionCatalog,
  getApplicationPermissionDiagnostics,
  getApplicationPermissionHistory,
  getRegisteredApplications,
  importPermissionManifestFromEndpoint,
  applyPermissionManifest,
  applyRemotePermissionManifest,
  previewPermissionManifest,
  previewRemotePermissionManifest,
  registerPermissionManifest,
  removeDelegatedMaintainer,
  updateRemovedPermissionReplacement,
} from './application-permissions-api';

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  },
}));

describe('application permissions api', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('lists registered applications with query parameters', async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 });

    await getRegisteredApplications({ page: 2, pageSize: 10, search: 'inventory' });

    expect(apiClient.get).toHaveBeenCalledWith('/api/admin/application-permissions/applications', {
      page: 2,
      pageSize: 10,
      search: 'inventory',
    });
  });

  it('registers an application permission manifest', async () => {
    const request = {
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
      ownerType: 'user',
      manifestBaseUrl: 'https://patient.example/api',
    };
    vi.mocked(apiClient.post).mockResolvedValueOnce({ id: 'application-1' });

    await registerPermissionManifest(request);

    expect(apiClient.post).toHaveBeenCalledWith('/api/admin/application-permissions/applications', request);
  });

  it('imports an application permission manifest from a well-known endpoint', async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({ applicationId: 'application-1' });

    await importPermissionManifestFromEndpoint('https://patient.example/.well-known/permissions');

    expect(apiClient.post).toHaveBeenCalledWith('/api/admin/application-permissions/applications/import', {
      endpoint: 'https://patient.example/.well-known/permissions',
    });
  });

  it('previews a manifest update with concurrency token', async () => {
    const request = {
      manifest: {
        schemaVersion: '1.0.0',
        application: { id: 'patient-api', displayName: 'Patient API', description: null, version: '2.0.0' },
        permissions: [],
      },
      ownerId: 'owner-1',
      ownerType: 'user',
      manifestBaseUrl: null,
    };
    vi.mocked(apiClient.post).mockResolvedValueOnce({ applicationId: 'application-1', removals: [] });

    await previewPermissionManifest('application-1', request, 3);

    expect(apiClient.post).toHaveBeenCalledWith('/api/admin/application-permissions/applications/application-1/manifest/preview', {
      manifest: request.manifest,
      concurrencyToken: 3,
    });
  });

  it('applies a manifest update and returns the destructive result envelope', async () => {
    const request = {
      manifest: {
        schemaVersion: '1.0.0',
        application: { id: 'patient-api', displayName: 'Patient API', description: null, version: '2.0.0' },
        permissions: [],
      },
      ownerId: 'owner-1',
      ownerType: 'user',
      manifestBaseUrl: null,
    };
    vi.mocked(apiClient.post).mockResolvedValueOnce({ application: { id: 'application-1' }, result: { removedPermissions: [] } });

    await applyPermissionManifest('application-1', request, 4);

    expect(apiClient.post).toHaveBeenCalledWith('/api/admin/application-permissions/applications/application-1/manifest', {
      manifest: request.manifest,
      concurrencyToken: 4,
    });
  });

  it('previews a remote manifest update with concurrency token', async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({ applicationId: 'application-1', removals: [] });

    await previewRemotePermissionManifest('application-1', 3);

    expect(apiClient.post).toHaveBeenCalledWith('/api/admin/application-permissions/applications/application-1/import/preview', {
      concurrencyToken: 3,
    });
  });

  it('applies a remote manifest update with concurrency token', async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({ application: { id: 'application-1' }, result: { removedPermissions: [] } });

    await applyRemotePermissionManifest('application-1', 4);

    expect(apiClient.post).toHaveBeenCalledWith('/api/admin/application-permissions/applications/application-1/import', {
      concurrencyToken: 4,
    });
  });

  it('lists assignable catalog permissions', async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 });

    await getAssignablePermissionCatalog({ page: 1, pageSize: 100 });

    expect(apiClient.get).toHaveBeenCalledWith('/api/admin/application-permissions/catalog', {
      page: 1,
      pageSize: 100,
    });
  });

  it('lists permission history with optional application filter', async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ deletedApplications: [], removedPermissions: [] });

    await getApplicationPermissionHistory({ applicationIdentifier: 'orders-api' });

    expect(apiClient.get).toHaveBeenCalledWith('/api/admin/application-permissions/history', {
      applicationIdentifier: 'orders-api',
    });
  });

  it('lists permission assignment diagnostics', async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ issues: [] });

    await getApplicationPermissionDiagnostics();

    expect(apiClient.get).toHaveBeenCalledWith('/api/admin/application-permissions/diagnostics');
  });

  it('updates removed permission replacement guidance', async () => {
    vi.mocked(apiClient.patch).mockResolvedValueOnce({ id: 'permission-1' });

    await updateRemovedPermissionReplacement('permission-1', {
      replacementFullPermissionKey: 'users:read',
      replacementNote: 'Use platform permission.',
      concurrencyToken: 8,
    });

    expect(apiClient.patch).toHaveBeenCalledWith('/api/admin/application-permissions/permissions/permission-1/replacement', {
      replacementFullPermissionKey: 'users:read',
      replacementNote: 'Use platform permission.',
      concurrencyToken: 8,
    });
  });

  it('removes maintainers with concurrency token query string', async () => {
    vi.mocked(apiClient.delete).mockResolvedValueOnce({ id: 'application-1' });

    await removeDelegatedMaintainer('application-1', 'owner@example.com', 7);

    expect(apiClient.delete).toHaveBeenCalledWith(
      '/api/admin/application-permissions/applications/application-1/maintainers/owner%40example.com?concurrencyToken=7'
    );
  });
});
