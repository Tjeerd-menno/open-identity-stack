import { Button, TextInput } from '@mantine/core';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { ActionBar, DestructiveActionDialog, FormSection } from './FormPrimitives';

describe('FormPrimitives', () => {
  it('renders a titled form section with description and fields', () => {
    renderManagementWeb(
      <FormSection title="Profile" description="Public operator metadata.">
        <TextInput label="Display name" />
      </FormSection>
    );

    expect(screen.getByRole('region', { name: 'Profile' })).toBeInTheDocument();
    expect(screen.getByText('Public operator metadata.')).toBeInTheDocument();
    expect(screen.getByLabelText('Display name')).toBeInTheDocument();
  });

  it('renders a reusable action bar with cancel and submit actions', async () => {
    const onCancel = vi.fn();
    const onSubmit = vi.fn();

    renderManagementWeb(
      <ActionBar
        cancelLabel="Back"
        submitLabel="Save changes"
        isSubmitting
        onCancel={onCancel}
        onSubmit={onSubmit}
      />
    );

    await userEvent.click(screen.getByRole('button', { name: 'Back' }));

    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('button', { name: 'Save changes' })).toBeDisabled();
  });

  it('wraps destructive confirmations with safe defaults', async () => {
    const onConfirm = vi.fn();
    const onOpenChange = vi.fn();

    renderManagementWeb(
      <DestructiveActionDialog
        opened
        onOpenChange={onOpenChange}
        onConfirm={onConfirm}
        subject="user"
        message="This disables the selected user."
      />
    );

    expect(screen.getByRole('dialog', { name: 'Delete user' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Delete user' }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('allows callers to provide extra actions without replacing submit behavior', async () => {
    const onSubmit = vi.fn();

    renderManagementWeb(
      <ActionBar
        submitLabel="Create"
        onSubmit={onSubmit}
        extraActions={<Button variant="subtle">Preview</Button>}
      />
    );

    expect(screen.getByRole('button', { name: 'Preview' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Create' }));

    expect(onSubmit).toHaveBeenCalledTimes(1);
  });
});
