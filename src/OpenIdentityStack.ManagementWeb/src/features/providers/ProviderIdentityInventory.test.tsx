import { screen } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, resetApiMocks } from '@/test/mock-api';
import { ProviderIdentityInventory } from './ProviderIdentityInventory';

vi.mock('@/lib/api', async () => {
  const { mockApi } = await import('@/test/mock-api');
  return { api: mockApi, getApiErrorMessage: (error: unknown) => String(error) };
});

it('shows recovery blockers without claiming password configuration proves safe access, and pages the inventory', async () => {
  resetApiMocks();
  mockApi.users.getIdentityMigrationInventory.mockResolvedValue({ items: [{ userId: 'u1', displayName: 'Affected user', status: 'Active', hasPasswordCredential: true, candidateFederationProviderIds: [], migrationBlocked: true, recoveryRequired: false, identities: [{ providerId: 'p1', providerName: 'Provider', subjectId: 'legacy', issuer: null, associationEvidence: 'Unknown', isQuarantined: true }] }], totalCount: 21, page: 1, pageSize: 20 });
  renderManagementWeb(<ProviderIdentityInventory providerId="p1" />, { auth: makeAuth() });
  expect(await screen.findByText('Migration blocked: independent association evidence required')).toBeInTheDocument();
  expect(screen.getByText(/configured; access must be tested independently/)).toBeInTheDocument();
  expect(screen.getByText('Quarantined · Evidence: Unknown')).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: /trust|approve/i })).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole('button', { name: 'Next inventory page' }));
  expect(mockApi.users.getIdentityMigrationInventory).toHaveBeenLastCalledWith({ providerId: 'p1', page: 2, pageSize: 20 });
});

it('identifies federation-only users requiring recovery before migration', async () => {
  resetApiMocks();
  mockApi.users.getIdentityMigrationInventory.mockResolvedValue({ items: [{ userId: 'u1', displayName: 'Affected user', status: 'Active', hasPasswordCredential: false, candidateFederationProviderIds: [], migrationBlocked: true, recoveryRequired: true, identities: [] }], totalCount: 1, page: 1, pageSize: 20 });
  renderManagementWeb(<ProviderIdentityInventory providerId="p1" />, { auth: makeAuth() });
  expect(await screen.findByText(/proof-based recovery must be delivered before migration/)).toBeInTheDocument();
});
