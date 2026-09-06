import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { Route, Routes } from 'react-router';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, resetApiMocks } from '@/test/mock-api';
import { ProviderDetailPage } from './ProviderDetailPage';

vi.mock('@/lib/api', async () => ({
  api: (await import('@/test/mock-api')).mockApi,
  getApiErrorMessage: (error: unknown) => String(error),
}));

beforeEach(() => {
  resetApiMocks();
  mockApi.providers.getProvider.mockResolvedValue({ id: 'p1', name: 'provider', displayName: 'Provider', authority: 'https://issuer.example', clientId: 'client', scopes: [], status: 'Active', createdAt: '2026-06-01T00:00:00Z', jitProvisioningEnabled: true, trustEmailVerification: false });
  mockApi.providers.setEmailVerificationTrust.mockResolvedValue(undefined);
});

it.each([true, false])('only provider writers can change email verification trust: %s', async (canWrite) => {
  const user = userEvent.setup();
  renderManagementWeb(<Routes><Route path="/providers/:providerId" element={<ProviderDetailPage />} /></Routes>, {
    auth: makeAuth({ permissions: canWrite ? ['providers:read', 'providers:write'] : ['providers:read'] }), initialEntries: ['/providers/p1'],
  });
  await user.click(await screen.findByRole('tab', { name: 'Settings' }));
  const toggle = screen.getByRole('switch', { name: 'Trust email verification' });
  expect(toggle).not.toBeChecked();
  if (canWrite) {
    await user.click(toggle);
    await waitFor(() => expect(mockApi.providers.setEmailVerificationTrust).toHaveBeenCalledWith('p1', true));
  } else {
    expect(toggle).toBeDisabled();
  }
});

it('requires explicit confirmation before withdrawing trust', async () => {
  mockApi.providers.getProvider.mockResolvedValue({ id: 'p1', name: 'provider', displayName: 'Provider', authority: 'https://issuer.example', clientId: 'client', scopes: [], status: 'Active', createdAt: '2026-06-01T00:00:00Z', jitProvisioningEnabled: true, trustEmailVerification: true });
  const user = userEvent.setup();
  renderManagementWeb(<Routes><Route path="/providers/:providerId" element={<ProviderDetailPage />} /></Routes>, {
    auth: makeAuth({ permissions: ['providers:read', 'providers:write'] }), initialEntries: ['/providers/p1'],
  });
  await user.click(await screen.findByRole('tab', { name: 'Settings' }));

  await user.click(screen.getByRole('switch', { name: 'Trust email verification' }));

  expect(mockApi.providers.setEmailVerificationTrust).not.toHaveBeenCalled();
  expect(screen.getByText(/existing proofs from this provider remain withdrawn/i)).toBeInTheDocument();
  expect(screen.getByText(/does not revoke existing credentials or sessions/i)).toBeInTheDocument();
  expect(screen.getByText(/open Sessions and revoke every active session/i)).toBeInTheDocument();
  expect(screen.getByText(/revoke known tokens or deny affected subjects until they expire/i)).toBeInTheDocument();
  expect(screen.getByRole('switch', { name: 'Trust email verification' })).toBeChecked();
  await user.click(screen.getByRole('button', { name: 'Withdraw trust' }));
  await waitFor(() => expect(mockApi.providers.setEmailVerificationTrust).toHaveBeenCalledWith('p1', false));
});
