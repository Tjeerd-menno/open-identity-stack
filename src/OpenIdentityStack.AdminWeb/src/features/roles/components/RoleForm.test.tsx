import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { RoleForm } from './RoleForm';

vi.mock('./PermissionSelector', () => ({
  PermissionSelector: ({
    disabled,
    onChange,
    selectedPermissions,
  }: {
    disabled?: boolean;
    onChange: (permissions: string[]) => void;
    selectedPermissions: string[];
  }) => (
    <div>
      <div data-testid="selected-permissions">{selectedPermissions.join(',')}</div>
      <button type="button" disabled={disabled} onClick={() => onChange(['users:read'])}>
        Pick read permission
      </button>
      <button type="button" disabled={disabled} onClick={() => onChange(['users:*'])}>
        Pick wildcard permission
      </button>
    </div>
  ),
}));

describe('RoleForm', () => {
  it('submits a new role with selected permissions', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<RoleForm onSubmit={onSubmit} onCancel={vi.fn()} />);

    await user.type(screen.getByLabelText(/^name$/i), 'auditor');
    await user.type(screen.getByLabelText(/display name/i), 'Auditor');
    await user.type(screen.getByLabelText(/description/i), 'Read-only role');
    await user.click(screen.getByRole('button', { name: /pick read permission/i }));
    await user.click(screen.getByRole('button', { name: /create role/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({
        name: 'auditor',
        displayName: 'Auditor',
        description: 'Read-only role',
        permissions: ['users:read'],
      });
    });
  });

  it('disables the immutable name field while editing', () => {
    render(
      <RoleForm
        role={{
          id: 'role-1',
          name: 'admin',
          displayName: 'Admin',
          description: 'System admin',
          isSystemRole: true,
          isActive: true,
          permissions: ['*'],
        }}
        onSubmit={vi.fn()}
        onCancel={vi.fn()}
      />
    );

    expect(screen.getByLabelText(/^name$/i)).toBeDisabled();
    expect(screen.getByTestId('selected-permissions')).toHaveTextContent('*');
    expect(screen.getByRole('button', { name: /update role/i })).toBeInTheDocument();
  });

  it('calls cancel without submitting', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    const onSubmit = vi.fn();
    render(<RoleForm onSubmit={onSubmit} onCancel={onCancel} />);

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(onCancel).toHaveBeenCalledOnce();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('confirms wildcard grants when saving instead of requiring an acknowledgement checkbox', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<RoleForm onSubmit={onSubmit} onCancel={vi.fn()} />);

    await user.type(screen.getByLabelText(/^name$/i), 'user-admin');
    await user.type(screen.getByLabelText(/display name/i), 'User Admin');
    await user.click(screen.getByRole('button', { name: /pick wildcard permission/i }));

    expect(screen.queryByRole('checkbox', { name: /acknowledge wildcard grant/i })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /create role/i }));

    expect(onSubmit).not.toHaveBeenCalled();
    expect(await screen.findByText(/permissions include wildcard permissions/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /save role/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({
        name: 'user-admin',
        displayName: 'User Admin',
        description: '',
        permissions: ['users:*'],
        acknowledgeWildcardGrant: true,
      });
    });
  });
});
