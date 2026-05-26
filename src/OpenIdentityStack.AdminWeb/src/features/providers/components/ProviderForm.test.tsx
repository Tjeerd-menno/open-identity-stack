import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ProviderForm } from './ProviderForm';

describe('ProviderForm', () => {
  it('submits provider settings with split scopes and omitted empty secret', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<ProviderForm onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText(/^name$/i), { target: { value: 'google' } });
    fireEvent.change(screen.getByLabelText(/display name/i), { target: { value: 'Google' } });
    fireEvent.change(screen.getByLabelText(/authority/i), { target: { value: 'https://accounts.google.com' } });
    fireEvent.change(screen.getByLabelText(/client id/i), { target: { value: 'client-id' } });
    fireEvent.change(screen.getByLabelText(/scopes/i), { target: { value: 'openid profile email api' } });
    await user.click(screen.getByRole('button', { name: /create provider/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({
        name: 'google',
        displayName: 'Google',
        authority: 'https://accounts.google.com',
        clientId: 'client-id',
        clientSecret: undefined,
        scopes: ['openid', 'profile', 'email', 'api'],
        jitProvisioningEnabled: true,
      });
    });
  });

  it('allows disabling just-in-time provisioning', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<ProviderForm onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText(/^name$/i), 'github');
    await user.type(screen.getByLabelText(/display name/i), 'GitHub');
    await user.type(screen.getByLabelText(/authority/i), 'https://github.example.com');
    await user.type(screen.getByLabelText(/client id/i), 'github-client');
    await user.click(screen.getByRole('checkbox', { name: /just-in-time provisioning/i }));
    await user.click(screen.getByRole('button', { name: /create provider/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
        jitProvisioningEnabled: false,
      }));
    });
  });

  it('does not submit invalid provider settings', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn();
    render(<ProviderForm onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText(/^name$/i), 'Invalid Name');
    await user.type(screen.getByLabelText(/authority/i), 'not-a-url');
    await user.click(screen.getByRole('button', { name: /create provider/i }));

    expect(await screen.findByText('Name must be lowercase alphanumeric with hyphens')).toBeInTheDocument();
    expect(screen.getByText('Authority must be a valid URL')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });
});
