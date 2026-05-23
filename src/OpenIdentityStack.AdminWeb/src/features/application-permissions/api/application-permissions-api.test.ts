import { describe, expect, it, vi, beforeEach } from 'vitest';
import { apiClient } from '@/lib/api/client';
import {
  getAssignablePermissionCatalog,
  getRegisteredApplications,
  importPermissionManifestFromEndpoint,
  registerPermissionManifest,
  removeDelegatedMaintainer,
} from './application-permissions-api';

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
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
    const manifest = {
      application: {
        id: 'patient-api',
        name: 'Patient API',
        version: '1.0.0',
      },
      permissions: [
        {
          name: 'read:patients',
          description: 'Allows reading patient data',
          category: 'Patients',
        },
      ],
    };
    vi.mocked(apiClient.post).mockResolvedValueOnce({ applicationId: 'application-1' });

    await registerPermissionManifest(manifest);

    expect(apiClient.post).toHaveBeenCalledWith('/api/admin/application-permissions/applications', manifest);
  });

  it('imports an application permission manifest from a well-known endpoint', async () => {
    vi.mocked(apiClient.post).mockResolvedValueOnce({ applicationId: 'application-1' });

    await importPermissionManifestFromEndpoint('https://patient.example/.well-known/permissions');

    expect(apiClient.post).toHaveBeenCalledWith('/api/admin/application-permissions/applications/import', {
      endpoint: 'https://patient.example/.well-known/permissions',
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

  it('removes maintainers with concurrency token query string', async () => {
    vi.mocked(apiClient.delete).mockResolvedValueOnce({ id: 'application-1' });

    await removeDelegatedMaintainer('application-1', 'owner@example.com', 7);

    expect(apiClient.delete).toHaveBeenCalledWith(
      '/api/admin/application-permissions/applications/application-1/maintainers/owner%40example.com?concurrencyToken=7'
    );
  });
});
