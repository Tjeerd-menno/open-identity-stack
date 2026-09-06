import { afterEach, expect, it, vi } from 'vitest';
import { createAdminApiClient } from './index';
import { createCutoverContract } from './cutover';
const originalFetch = globalThis.fetch;
afterEach(() => { globalThis.fetch = originalFetch; });
it('sends no identity identifiers for emergency proof and preserves explicit cutover operation IDs', async () => {
  const fetch = vi.fn().mockResolvedValue(new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }));
  globalThis.fetch = fetch;
  const contract = createCutoverContract(createAdminApiClient({ baseUrl: 'https://admin.example' }));
  await contract.recordEmergencyAccess();
  expect(fetch.mock.calls[0][0]).toBe('https://admin.example/api/admin/security/emergency-access-evidence');
  expect(fetch.mock.calls[0][1].body).toBe('{}');
  fetch.mockResolvedValue(new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }));
  await contract.execute('stable-operation');
  expect(JSON.parse(fetch.mock.calls[1][1].body)).toEqual({ operationId: 'stable-operation' });
});
it('matches the no-content resource review response', async () => {
  globalThis.fetch = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
  const contract = createCutoverContract(createAdminApiClient({ baseUrl: 'https://admin.example' }));
  await expect(contract.reviewResourceWindow('resource', { mechanism: 'OfflineExpiry', residualSeconds: 3600, evidenceReference: 'change-record' })).resolves.toBeUndefined();
});
