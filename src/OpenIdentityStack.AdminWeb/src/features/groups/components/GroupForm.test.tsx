import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { GroupForm } from './GroupForm';

describe('GroupForm', () => {
  it('submits new group details', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<GroupForm onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText(/group name/i), 'Developers');
    await user.type(screen.getByLabelText(/description/i), 'People who build the platform');
    await user.click(screen.getByRole('button', { name: /create group/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({
        name: 'Developers',
        description: 'People who build the platform',
      });
    });
  });

  it('prefills edit mode and calls cancel', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    render(
      <GroupForm
        group={{
          id: 'group-1',
          name: 'Admins',
          description: 'System administrators',
          memberCount: 2,
          mappingCount: 3,
          createdAt: '2026-01-01',
          modifiedAt: null,
        }}
        onSubmit={vi.fn().mockResolvedValue(undefined)}
        onCancel={onCancel}
      />
    );

    expect(screen.getByLabelText(/group name/i)).toHaveValue('Admins');
    expect(screen.getByLabelText(/description/i)).toHaveValue('System administrators');

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('prevents submitting without a group name', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<GroupForm onSubmit={onSubmit} />);

    await user.click(screen.getByRole('button', { name: /create group/i }));

    expect(await screen.findByText('Group name is required')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
