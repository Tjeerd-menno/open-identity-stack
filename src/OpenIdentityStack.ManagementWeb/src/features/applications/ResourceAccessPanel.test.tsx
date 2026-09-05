import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { mockApi, resetApiMocks } from '@/test/mock-api';
import { ResourceAccessPanel } from './ResourceAccessPanel';

vi.mock('@/lib/api', async () => {
  const { mockApi } = await import('@/test/mock-api');
  return { api: mockApi, getApiErrorMessage: (error: unknown) => String(error) };
});

beforeEach(() => {
  resetApiMocks();
  mockApi.applications.listProtectedResources.mockResolvedValue([
    { id: 'resource', displayName: 'Orders API', audience: 'https://orders.example.com', scope: 'orders', permissionNamespaces: ['orders'], enabled: true, revision: 2, isAdministrative: false },
    { id: 'admin', displayName: 'Admin API', audience: 'urn:openidentitystack:admin-api', scope: 'ois.admin', permissionNamespaces: ['openidentitystack'], enabled: true, revision: 1, isAdministrative: true },
  ]);
  mockApi.applications.listClientResourceGrants.mockResolvedValue([
    { resourceId: 'resource', delegatedPermissions: ['orders:invoice:read'], applicationPermissions: [], revision: 3 },
  ]);
});

it('saves delegated and machine permissions separately with the observed revision', async () => {
  const user = userEvent.setup();
  mockApi.applications.configureClientResourceGrant.mockResolvedValue({ revision: 4 });
  renderManagementWeb(<ResourceAccessPanel applicationId="client" canWrite />);
  await waitFor(() => expect(screen.getByRole('combobox', { name: 'Protected resource' })).toBeEnabled());
  await user.click(screen.getByRole('combobox', { name: 'Protected resource' }));
  await user.keyboard('{ArrowDown}{Enter}');
  await user.type(screen.getByLabelText('Application permissions'), 'orders:invoice:write');
  await user.click(screen.getByRole('button', { name: 'Save resource grant' }));
  await waitFor(() => expect(mockApi.applications.configureClientResourceGrant).toHaveBeenCalledWith('client', 'resource', {
    delegatedPermissions: ['orders:invoice:read'], applicationPermissions: ['orders:invoice:write'], expectedRevision: 3,
  }));
});

it('requires the dedicated administrative workflow even for application writers', async () => {
  const user = userEvent.setup();
  renderManagementWeb(<ResourceAccessPanel applicationId="client" canWrite />);
  await waitFor(() => expect(screen.getByRole('combobox', { name: 'Protected resource' })).toBeEnabled());
  await user.click(screen.getByRole('combobox', { name: 'Protected resource' }));
  await user.keyboard('{ArrowDown}{ArrowDown}{Enter}');
  expect(screen.getByText('Administrative access requires its dedicated approval workflow.')).toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Save resource grant' })).not.toBeInTheDocument();
});

it('hides mutation controls for read-only operators', async () => {
  renderManagementWeb(<ResourceAccessPanel applicationId="client" canWrite={false} />);
  await waitFor(() => expect(screen.getByRole('combobox', { name: 'Protected resource' })).toBeEnabled());
  expect(screen.queryByRole('button', { name: 'Add protected resource' })).not.toBeInTheDocument();
});
