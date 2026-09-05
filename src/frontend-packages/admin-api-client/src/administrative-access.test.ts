import { afterEach, describe, expect, it, vi } from 'vitest';
import { createAdminApiClient, createAdministrativeAccessContract } from './index';

const originalFetch = globalThis.fetch;
afterEach(() => { globalThis.fetch = originalFetch; });

describe('administrative access contract', () => {
  it('reads explicit entitlement and sends separate ceilings with a concurrency revision', async () => {
    const response = { approved: true, delegatedPermissions: ['users:read'], applicationPermissions: ['audit-logs:read'], revision: 7 };
    const fetchMock = vi.fn().mockImplementation(async () => new Response(JSON.stringify(response), { status: 200 }));
    globalThis.fetch = fetchMock;
    const contract = createAdministrativeAccessContract(createAdminApiClient({ baseUrl: 'https://admin.example' }));
    expect(await contract.get('client')).toEqual(response);
    await contract.save('client', { delegatedPermissions: ['users:read'], applicationPermissions: [], expectedRevision: 7 });
    expect(fetchMock.mock.calls[1][0]).toBe('https://admin.example/api/admin/applications/client/administrative-access');
    expect(fetchMock.mock.calls[1][1].method).toBe('PUT');
    expect(JSON.parse(fetchMock.mock.calls[1][1].body)).toEqual({ delegatedPermissions: ['users:read'], applicationPermissions: [], expectedRevision: 7 });
  });

  it('does not retry an entitlement revision conflict as approval', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify({ error: 'Conflict.AdministrativeAccess.Conflict', message: 'Reload before saving.' }), { status: 409 }));
    const approve = vi.fn().mockResolvedValue(true);
    const contract = createAdministrativeAccessContract(createAdminApiClient({ baseUrl: 'https://admin.example', onAdministrativeApprovalRequired: approve }));
    await expect(contract.save('client', { delegatedPermissions: [], applicationPermissions: [], expectedRevision: 1 })).rejects.toMatchObject({ status: 409 });
    expect(approve).not.toHaveBeenCalled();
  });
});
