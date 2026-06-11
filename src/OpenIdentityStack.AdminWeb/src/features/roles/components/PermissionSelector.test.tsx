import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';
import { PermissionSelector } from './PermissionSelector';

const { useAssignablePermissionCatalog } = vi.hoisted(() => ({
  useAssignablePermissionCatalog: vi.fn(),
}));

const { getPlatformPermissionCatalog } = vi.hoisted(() => ({
  getPlatformPermissionCatalog: vi.fn(),
}));

vi.mock('@/features/application-permissions/hooks', () => ({
  useAssignablePermissionCatalog,
}));

vi.mock('../api/roles-api', () => ({
  getPlatformPermissionCatalog,
}));

function renderSelector(ui: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('PermissionSelector', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAssignablePermissionCatalog.mockReturnValue({ data: undefined });
    getPlatformPermissionCatalog.mockResolvedValue({
      items: [
        { permission: 'users:read', resource: 'users', action: 'read', kind: 'concrete', displayName: 'Read Users', assignable: true },
        { permission: 'users:create', resource: 'users', action: 'create', kind: 'concrete', displayName: 'Create Users', assignable: true },
        { permission: 'roles:read', resource: 'roles', action: 'read', kind: 'concrete', displayName: 'Read Roles', assignable: true },
        { permission: 'providers:delete', resource: 'providers', action: 'delete', kind: 'concrete', displayName: 'Delete Providers', assignable: true },
      ],
    });
  });

  it('checks selected permissions and emits added permissions', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    renderSelector(<PermissionSelector selectedPermissions={['users:read']} onChange={onChange} />);

    expect(screen.getByLabelText('Read Users')).toBeChecked();

    await user.click(screen.getByLabelText('Create Users'));

    expect(onChange).toHaveBeenCalledWith(['users:read', 'users:create']);
  });

  it('emits removed permissions', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    renderSelector(<PermissionSelector selectedPermissions={['users:read', 'roles:read']} onChange={onChange} />);

    await user.click(screen.getByLabelText('Read Users'));

    expect(onChange).toHaveBeenCalledWith(['roles:read']);
  });

  it('visualizes concrete permissions covered by a selected wildcard as checked', async () => {
    getPlatformPermissionCatalog.mockResolvedValue({
      items: [
        { permission: 'users:*', resource: 'users', action: '*', kind: 'wildcard', displayName: 'All Users', assignable: true },
        { permission: 'users:read', resource: 'users', action: 'read', kind: 'concrete', displayName: 'Read Users', assignable: true },
        { permission: 'users:create', resource: 'users', action: 'create', kind: 'concrete', displayName: 'Create Users', assignable: true },
        { permission: 'roles:read', resource: 'roles', action: 'read', kind: 'concrete', displayName: 'Read Roles', assignable: true },
      ],
    });

    renderSelector(<PermissionSelector selectedPermissions={['users:*']} onChange={vi.fn()} />);

    expect(await screen.findByLabelText('All Users')).toBeChecked();
    expect(screen.getByLabelText('Read Users')).toBeChecked();
    expect(screen.getByLabelText('Create Users')).toBeChecked();
    expect(screen.getByLabelText('Read Roles')).not.toBeChecked();
  });

  it('adds registered application permissions when the catalog is available', async () => {
    const user = userEvent.setup();
    useAssignablePermissionCatalog.mockReturnValue({
      data: {
        items: [
          {
            fullPermissionKey: 'inventory:read',
            displayName: 'Read inventory',
            applicationId: 'inventory-api',
            applicationName: 'Inventory API',
          },
        ],
      },
    });

    renderSelector(<PermissionSelector selectedPermissions={['inventory:read']} onChange={vi.fn()} />);

    await user.click(screen.getByRole('tab', { name: 'Inventory API' }));

    expect(screen.getByLabelText('Read inventory')).toBeChecked();
  });

  it('shows application tabs with built-in permissions first', async () => {
    const user = userEvent.setup();
    useAssignablePermissionCatalog.mockReturnValue({
      data: {
        items: [
          {
            fullPermissionKey: 'read:patients',
            displayName: 'read:patients',
            description: 'Allows reading patient data',
            category: 'Patients',
            applicationId: 'patient-api',
            applicationName: 'Patient API',
          },
        ],
      },
    });

    renderSelector(<PermissionSelector selectedPermissions={['read:patients']} onChange={vi.fn()} />);

    expect(screen.getAllByRole('tab')[0]).toHaveTextContent('Built-in');
    await user.click(screen.getByRole('tab', { name: 'Patient API' }));

    expect(screen.getByLabelText('read:patients')).toBeChecked();
    expect(screen.getByText('Allows reading patient data')).toBeInTheDocument();
    expect(screen.getByText('Patients')).toBeInTheDocument();
  });

  it('disables all checkboxes when requested', () => {
    renderSelector(<PermissionSelector selectedPermissions={[]} onChange={vi.fn()} disabled />);

    expect(screen.getByLabelText('Read Users')).toBeDisabled();
    expect(screen.getByLabelText('Delete Providers')).toBeDisabled();
  });
});
