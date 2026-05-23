import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ConfirmDialog } from './ConfirmDialog';

describe('ConfirmDialog', () => {
  it('shows the title and description when open', () => {
    render(
      <ConfirmDialog
        open
        onOpenChange={vi.fn()}
        title="Delete user"
        description="This action cannot be undone."
        onConfirm={vi.fn()}
      />
    );

    expect(screen.getByText('Delete user')).toBeInTheDocument();
    expect(screen.getByText('This action cannot be undone.')).toBeInTheDocument();
  });

  it('calls onOpenChange(false) when cancel is clicked', async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();

    render(
      <ConfirmDialog
        open
        onOpenChange={onOpenChange}
        title="Delete user"
        description="This action cannot be undone."
        onConfirm={vi.fn()}
      />
    );

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('calls onConfirm when confirm is clicked', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();

    render(
      <ConfirmDialog
        open
        onOpenChange={vi.fn()}
        title="Delete user"
        description="This action cannot be undone."
        confirmLabel="Delete"
        onConfirm={onConfirm}
      />
    );

    await user.click(screen.getByRole('button', { name: /delete/i }));

    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it('disables actions and shows a processing state while loading', () => {
    render(
      <ConfirmDialog
        open
        onOpenChange={vi.fn()}
        title="Delete user"
        description="This action cannot be undone."
        onConfirm={vi.fn()}
        loading
      />
    );

    expect(screen.getByRole('button', { name: /cancel/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /processing/i })).toBeDisabled();
  });

  it('applies destructive styling to the confirm button when requested', () => {
    render(
      <ConfirmDialog
        open
        onOpenChange={vi.fn()}
        title="Delete user"
        description="This action cannot be undone."
        confirmLabel="Delete"
        onConfirm={vi.fn()}
        variant="destructive"
      />
    );

    expect(screen.getByRole('button', { name: /delete/i })).toHaveClass('bg-destructive');
  });
});
