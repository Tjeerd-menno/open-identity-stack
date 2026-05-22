import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PermissionSelector } from './PermissionSelector';

const { useAssignablePermissionCatalog } = vi.hoisted(() => ({
  useAssignablePermissionCatalog: vi.fn(),
}));

vi.mock('@/features/service-permissions/hooks', () => ({
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

  it('adds registered service permissions when the catalog is available', () => {
    useAssignablePermissionCatalog.mockReturnValue({
      data: {
        items: [
          {
            fullPermissionKey: 'inventory:read',
            displayName: 'Read inventory',
          },
        ],
      },
    });

    render(<PermissionSelector selectedPermissions={['inventory:read']} onChange={vi.fn()} />);

    expect(screen.getByText('Registered Services')).toBeInTheDocument();
    expect(screen.getByLabelText('Read inventory')).toBeChecked();
  });

  it('disables all checkboxes when requested', () => {
    render(<PermissionSelector selectedPermissions={[]} onChange={vi.fn()} disabled />);

    expect(screen.getByLabelText('Read Users')).toBeDisabled();
    expect(screen.getByLabelText('Delete Providers')).toBeDisabled();
  });
});
