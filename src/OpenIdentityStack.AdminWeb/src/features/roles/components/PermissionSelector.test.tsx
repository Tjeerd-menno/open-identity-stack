import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PermissionSelector } from './PermissionSelector';

const { useAssignablePermissionCatalog } = vi.hoisted(() => ({
  useAssignablePermissionCatalog: vi.fn(),
}));

vi.mock('@/features/application-permissions/hooks', () => ({
  useAssignablePermissionCatalog,
}));

describe('PermissionSelector', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAssignablePermissionCatalog.mockReturnValue({ data: undefined });
  });

  it('checks selected permissions and emits added permissions', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<PermissionSelector selectedPermissions={['users:read']} onChange={onChange} />);

    expect(screen.getByLabelText('Read Users')).toBeChecked();

    await user.click(screen.getByLabelText('Create Users'));

    expect(onChange).toHaveBeenCalledWith(['users:read', 'users:create']);
  });

  it('emits removed permissions', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<PermissionSelector selectedPermissions={['users:read', 'roles:read']} onChange={onChange} />);

    await user.click(screen.getByLabelText('Read Users'));

    expect(onChange).toHaveBeenCalledWith(['roles:read']);
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

    render(<PermissionSelector selectedPermissions={['inventory:read']} onChange={vi.fn()} />);

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

    render(<PermissionSelector selectedPermissions={['read:patients']} onChange={vi.fn()} />);

    expect(screen.getAllByRole('tab')[0]).toHaveTextContent('Built-in');
    await user.click(screen.getByRole('tab', { name: 'Patient API' }));

    expect(screen.getByLabelText('read:patients')).toBeChecked();
    expect(screen.getByText('Allows reading patient data')).toBeInTheDocument();
    expect(screen.getByText('Patients')).toBeInTheDocument();
  });

  it('disables all checkboxes when requested', () => {
    render(<PermissionSelector selectedPermissions={[]} onChange={vi.fn()} disabled />);

    expect(screen.getByLabelText('Read Users')).toBeDisabled();
    expect(screen.getByLabelText('Delete Providers')).toBeDisabled();
  });
});
