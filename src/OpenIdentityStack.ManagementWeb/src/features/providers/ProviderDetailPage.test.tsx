import { screen } from '@testing-library/react';
import { expect, it, vi } from 'vitest';
import { Route, Routes } from 'react-router';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, resetApiMocks } from '@/test/mock-api';
import { ProviderDetailPage } from './ProviderDetailPage';

vi.mock('@/lib/api', async () => {
  const { mockApi } = await import('@/test/mock-api');
  return { api: mockApi, getApiErrorMessage: (error: unknown) => String(error) };
});

it('explains issuer replacement without offering an authority edit', async () => {
  resetApiMocks();
  mockApi.users.getIdentityMigrationInventory.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  mockApi.providers.getProvider.mockResolvedValue({ id: 'p1', name: 'provider', displayName: 'Provider', authority: 'https://discovery.example', clientId: 'client', scopes: ['openid'], jitProvisioningEnabled: true, status: 'Active', createdAt: '2026-09-05T00:00:00Z' });
  renderManagementWeb(<Routes><Route path="/providers/:providerId" element={<ProviderDetailPage />} /></Routes>, { auth: makeAuth(), initialEntries: ['/providers/p1'] });
  expect(await screen.findByText(/Register a new provider and explicitly migrate identities/)).toBeInTheDocument();
  expect(screen.getByText('Discovery authority')).toBeInTheDocument();
  expect(screen.queryByRole('textbox', { name: /authority/i })).not.toBeInTheDocument();
});


it('does not request user identity inventory without users read permission', async () => {
  resetApiMocks();
  mockApi.providers.getProvider.mockResolvedValue({ id: 'p1', name: 'provider', displayName: 'Provider', authority: 'https://discovery.example', clientId: 'client', scopes: ['openid'], jitProvisioningEnabled: true, status: 'Active', createdAt: '2026-09-05T00:00:00Z' });
  renderManagementWeb(<Routes><Route path="/providers/:providerId" element={<ProviderDetailPage />} /></Routes>, { auth: makeAuth({ permissions: ['providers:read'] }), initialEntries: ['/providers/p1'] });
  expect(await screen.findByText('Discovery authority')).toBeInTheDocument();
  expect(screen.queryByText('Identity migration inventory')).not.toBeInTheDocument();
  expect(mockApi.users.getIdentityMigrationInventory).not.toHaveBeenCalled();
});
