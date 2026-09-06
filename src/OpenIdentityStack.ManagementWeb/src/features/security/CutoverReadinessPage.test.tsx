import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import type { CutoverPreflight } from '@openidentitystack/admin-api-client';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, resetApiMocks } from '@/test/mock-api';
import { credentialCutoverOperationIdKey, credentialCutoverSubmittedOperationIdKey, CutoverReadinessPage } from './CutoverReadinessPage';

vi.mock('@/lib/api', async () => {
  const { mockApi } = await import('@/test/mock-api');
  return { api: mockApi, getApiErrorMessage: (error: unknown) => String(error) };
});
const ready: CutoverPreflight = {
  epoch: 'epoch', evaluatedAt: '2026-09-05T00:00:00Z', ready: true, blockers: [], emergencyAccess: null,
  identities: { quarantinedLinks: 0, affectedUsers: 0, federationOnlyUsers: 0, passwordCandidates: 0, disabledUsers: 0, verifiedEmails: 0, providerEvidence: 0, withdrawnEvidence: 0 },
  administrativeClients: [], businessResources: [], outstandingAccessTokens: 0, latestAccessTokenExpiry: null,
};
beforeEach(() => {
  resetApiMocks();
  sessionStorage.removeItem(credentialCutoverOperationIdKey);
  sessionStorage.removeItem(credentialCutoverSubmittedOperationIdKey);
});
it('keeps quarantined identities blocked even with a password candidate and acknowledgement', async () => {
  mockApi.cutover.getReadiness.mockResolvedValue({ ...ready, ready: false, identities: { ...ready.identities, quarantinedLinks: 1, passwordCandidates: 1 }, blockers: [{ code: 'Identity.Quarantined', message: 'Quarantined identities require a separate recovery design.', count: 1 }] });
  renderManagementWeb(<CutoverReadinessPage />, { auth: makeAuth() });
  expect(await screen.findByText('Quarantined identities require a separate recovery design.')).toBeInTheDocument();
  expect(screen.getByText(/A configured password is only a candidate/)).toBeInTheDocument();
  fireEvent.click(screen.getByRole('checkbox', { name: /I accept/ }));
  expect(screen.getByRole('button', { name: 'Execute credential cutover' })).toBeDisabled();
  expect(mockApi.cutover.execute).not.toHaveBeenCalled();
});
it('records only the authenticated local session and preserves the block on failure', async () => {
  mockApi.cutover.getReadiness.mockResolvedValue({ ...ready, ready: false });
  mockApi.cutover.recordEmergencyAccess.mockRejectedValue(new Error('A fresh local password login is required'));
  renderManagementWeb(<CutoverReadinessPage />, { auth: makeAuth() });
  fireEvent.click(await screen.findByRole('button', { name: 'Verify my emergency access' }));
  expect(await screen.findByText(/A fresh local password login is required/)).toBeInTheDocument();
  expect(mockApi.cutover.recordEmergencyAccess).toHaveBeenCalledWith();
  expect(screen.getByRole('button', { name: 'Execute credential cutover' })).toBeDisabled();
});
it('shows delegated and application permissions for administrative clients', async () => {
  mockApi.cutover.getReadiness.mockResolvedValue({
    ...ready,
    administrativeClients: [{
      id: 'application-id', clientId: 'machine-client', active: true, approved: true,
      delegatedPermissions: ['users:read'], applicationPermissions: ['sessions:revoke'], requiresMigrationReview: false,
    }],
  });
  renderManagementWeb(<CutoverReadinessPage />, { auth: makeAuth() });
  expect(await screen.findByText('users:read')).toBeInTheDocument();
  expect(screen.getByText('sessions:revoke')).toBeInTheDocument();
});
it('requires acknowledgement and retains the operation ID when the live server gate rejects', async () => {
  mockApi.cutover.getReadiness.mockResolvedValue(ready);
  mockApi.cutover.execute.mockRejectedValue(new Error('Prerequisites changed; refresh readiness'));
  renderManagementWeb(<CutoverReadinessPage />, { auth: makeAuth() });
  const execute = await screen.findByRole('button', { name: 'Execute credential cutover' });
  expect(execute).toBeDisabled();
  fireEvent.click(screen.getByRole('checkbox', { name: /I accept/ }));
  fireEvent.click(execute);
  expect(await screen.findByText(/Prerequisites changed/)).toBeInTheDocument();
  fireEvent.click(execute);
  await waitFor(() => expect(mockApi.cutover.execute).toHaveBeenCalledTimes(2));
  expect(mockApi.cutover.execute.mock.calls[0][0]).toBe(mockApi.cutover.execute.mock.calls[1][0]);
});

it('does not treat a cancelled approval challenge as a submitted cutover', async () => {
  mockApi.cutover.getReadiness.mockResolvedValue(ready);
  mockApi.cutover.execute.mockRejectedValue(new Error('Approval cancelled'));
  const firstRender = renderManagementWeb(<CutoverReadinessPage />, { auth: makeAuth() });
  fireEvent.click(await screen.findByRole('checkbox', { name: /I accept/ }));
  fireEvent.click(screen.getByRole('button', { name: 'Execute credential cutover' }));
  expect(await screen.findByText(/Approval cancelled/)).toBeInTheDocument();
  expect(sessionStorage.getItem(credentialCutoverSubmittedOperationIdKey)).toBeNull();

  firstRender.unmount();
  renderManagementWeb(<CutoverReadinessPage />, { auth: makeAuth() });
  expect(await screen.findByRole('button', { name: 'Execute credential cutover' })).toBeDisabled();
});

it('retains the operation ID across auth recovery and clears it after confirmed success', async () => {
  mockApi.cutover.getReadiness
    .mockResolvedValueOnce(ready)
    .mockResolvedValue({ ...ready, ready: false, blockers: [{ code: 'Cutover.AlreadyCommitted', message: 'Current readiness was reset by the committed cutover.', count: 1 }] });
  mockApi.cutover.execute
    .mockImplementationOnce(async (_operationId: string, onApprovalRetry: () => void) => {
      onApprovalRetry();
      throw new Error('The response was lost');
    })
    .mockImplementationOnce(async (operationId: string) => ({ operationId, tokens: 2, grants: 1, sessions: 1 }));

  const firstRender = renderManagementWeb(<CutoverReadinessPage />, { auth: makeAuth() });
  fireEvent.click(await screen.findByRole('checkbox', { name: /I accept/ }));
  fireEvent.click(screen.getByRole('button', { name: 'Execute credential cutover' }));
  expect(await screen.findByText(/response was lost/i)).toBeInTheDocument();
  const firstOperationId = mockApi.cutover.execute.mock.calls[0][0];
  expect(sessionStorage.getItem(credentialCutoverOperationIdKey)).toBe(firstOperationId);
  expect(sessionStorage.getItem(credentialCutoverSubmittedOperationIdKey)).toBe(firstOperationId);

  firstRender.unmount();
  renderManagementWeb(<CutoverReadinessPage />, { auth: makeAuth() });
  const retry = await screen.findByRole('button', { name: 'Retry credential cutover' });
  expect(retry).toBeEnabled();
  fireEvent.click(retry);
  await screen.findByText(/Credential cutover completed/);

  expect(mockApi.cutover.execute.mock.calls[1][0]).toBe(firstOperationId);
  expect(sessionStorage.getItem(credentialCutoverOperationIdKey)).toBeNull();
  expect(sessionStorage.getItem(credentialCutoverSubmittedOperationIdKey)).toBeNull();
});
