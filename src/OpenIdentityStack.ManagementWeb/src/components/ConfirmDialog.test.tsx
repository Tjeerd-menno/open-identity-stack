import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { ConfirmDialog } from './ConfirmDialog';

describe('ConfirmDialog', () => {
  it('confirms destructive actions from a modal dialog', async () => {
    const onConfirm = vi.fn();
    const onOpenChange = vi.fn();

    renderManagementWeb(
      <ConfirmDialog
        opened
        onOpenChange={onOpenChange}
        onConfirm={onConfirm}
        title="Delete application"
        message="This action cannot be undone."
        confirmLabel="Delete"
      />
    );

    expect(screen.getByRole('dialog', { name: 'Delete application' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Delete' }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
  });
});
