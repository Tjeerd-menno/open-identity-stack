import { describe, expect, it, vi, beforeEach } from 'vitest';
import { apiClient } from '@/lib/api/client';
import {
  getAssignablePermissionCatalog,
  getRegisteredServices,
  registerService,
  removeDelegatedMaintainer,
} from './service-permissions-api';

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

describe('service permissions api', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('lists registered services with query parameters', async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 });

    await getRegisteredServices({ page: 2, pageSize: 10, search: 'inventory' });

    expect(apiClient.get).toHaveBeenCalledWith('/api/admin/service-permissions/services', {
      page: 2,
      pageSize: 10,
      search: 'inventory',
    });
  });

  it('registers a service with initial permissions', async () => {
    const request = {
      serviceIdentifier: 'inventory-api',
      displayName: 'Inventory API',
      description: '',
      ownerId: 'owner-1',
      ownerType: 'User',
      permissions: [{ permissionKey: 'read', displayName: 'Read inventory' }],
    };
    vi.mocked(apiClient.post).mockResolvedValueOnce({ serviceId: 'service-1' });

    await registerService(request);

    expect(apiClient.post).toHaveBeenCalledWith('/api/admin/service-permissions/services', request);
  });

  it('lists assignable catalog permissions', async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 });

    await getAssignablePermissionCatalog({ page: 1, pageSize: 100 });

    expect(apiClient.get).toHaveBeenCalledWith('/api/admin/service-permissions/catalog', {
      page: 1,
      pageSize: 100,
    });
  });

  it('removes maintainers with concurrency token query string', async () => {
    vi.mocked(apiClient.delete).mockResolvedValueOnce({ id: 'service-1' });

    await removeDelegatedMaintainer('service-1', 'owner@example.com', 7);

    expect(apiClient.delete).toHaveBeenCalledWith(
      '/api/admin/service-permissions/services/service-1/maintainers/owner%40example.com?concurrencyToken=7'
    );
  });
});
