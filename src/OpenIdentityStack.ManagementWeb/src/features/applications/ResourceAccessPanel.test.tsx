import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
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

it('prevents switching resources while a grant save is pending', async () => {
  const user = userEvent.setup();
  let completeSave!: (value: { revision: number }) => void;
  mockApi.applications.configureClientResourceGrant.mockReturnValue(new Promise((resolve) => { completeSave = resolve; }));
  renderManagementWeb(<ResourceAccessPanel applicationId="client" canWrite />);
  const selector = screen.getByRole('combobox', { name: 'Protected resource' });
  await waitFor(() => expect(selector).toBeEnabled());
  await user.click(selector);
  await user.keyboard('{ArrowDown}{Enter}');

  await user.click(screen.getByRole('button', { name: 'Save resource grant' }));

  await waitFor(() => expect(selector).toBeDisabled());
  completeSave({ revision: 4 });
  await waitFor(() => expect(selector).toBeEnabled());
});

it('disambiguates resources with the same display name by audience and scope', async () => {
  const user = userEvent.setup();
  mockApi.applications.listProtectedResources.mockResolvedValue([
    { id: 'orders-read', displayName: 'Orders API', audience: 'https://orders.example.com', scope: 'orders.read', permissionNamespaces: ['orders'], enabled: true, revision: 1, isAdministrative: false },
    { id: 'orders-write', displayName: 'Orders API', audience: 'https://fulfillment.example.com', scope: 'orders.write', permissionNamespaces: ['orders'], enabled: true, revision: 1, isAdministrative: false },
  ]);
  renderManagementWeb(<ResourceAccessPanel applicationId="client" canWrite />);
  const selector = screen.getByRole('combobox', { name: 'Protected resource' });
  await waitFor(() => expect(selector).toBeEnabled());

  await user.click(selector);

  expect(screen.getByRole('option', { name: 'Orders API — https://orders.example.com — orders.read' })).toBeInTheDocument();
  expect(screen.getByRole('option', { name: 'Orders API — https://fulfillment.example.com — orders.write' })).toBeInTheDocument();
});

it('clears grant editing state while grants for a different application load', async () => {
  const user = userEvent.setup();
  let completeSecondLoad!: (value: Array<{ resourceId: string; delegatedPermissions: string[]; applicationPermissions: string[]; revision: number }>) => void;
  mockApi.applications.listClientResourceGrants.mockImplementation((applicationId) => applicationId === 'client-one'
    ? Promise.resolve([{ resourceId: 'resource', delegatedPermissions: ['orders:first:read'], applicationPermissions: [], revision: 3 }])
    : new Promise((resolve) => { completeSecondLoad = resolve; }));
  function SwitchingPanel() {
    const [applicationId, setApplicationId] = useState('client-one');
    return <>
      <button onClick={() => setApplicationId('client-two')}>Switch application</button>
      <ResourceAccessPanel applicationId={applicationId} canWrite />
    </>;
  }
  renderManagementWeb(<SwitchingPanel />);
  const selector = screen.getByRole('combobox', { name: 'Protected resource' });
  await waitFor(() => expect(selector).toBeEnabled());
  await user.click(selector);
  await user.keyboard('{ArrowDown}{Enter}');
  expect(screen.getByLabelText('Delegated permission ceiling')).toHaveValue('orders:first:read');
  await user.click(screen.getByRole('button', { name: 'Add protected resource' }));
  expect(screen.getByRole('dialog', { name: 'Add protected resource' })).toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: 'Switch application' }));

  const switchedSelector = screen.getByRole('combobox', { name: 'Protected resource' });
  expect(switchedSelector).toHaveValue('');
  expect(screen.queryByRole('dialog', { name: 'Add protected resource' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Save resource grant' })).not.toBeInTheDocument();
  completeSecondLoad([{ resourceId: 'resource', delegatedPermissions: ['orders:second:read'], applicationPermissions: [], revision: 9 }]);
  await waitFor(() => expect(switchedSelector).toBeEnabled());
  await user.click(switchedSelector);
  await user.keyboard('{ArrowDown}{Enter}');
  expect(screen.getByLabelText('Delegated permission ceiling')).toHaveValue('orders:second:read');
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
