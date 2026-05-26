import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { UserStatus, type User } from '@/types';
import { UserForm } from './UserForm';

const existingUser: User = {
  id: 'user-1',
  email: 'jane@example.com',
  displayName: 'Jane Existing',
  status: UserStatus.Active,
  mfaEnabled: false,
  lastLoginAt: null,
  createdAt: '2026-01-01T00:00:00Z',
  modifiedAt: null,
  profile: {
    givenName: 'Jane',
    familyName: 'Existing',
    middleName: null,
    nickname: 'JJ',
    preferredUsername: 'jane.existing',
    profile: 'https://example.com/jane',
    picture: 'https://example.com/jane.png',
    website: 'https://jane.example.com',
    gender: 'female',
    birthdate: '1990-01-31',
    zoneInfo: 'Europe/Amsterdam',
    locale: 'en-US',
  },
};

describe('UserForm', () => {
  it('submits create user data with normalized profile values', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<UserForm onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'john@example.com' } });
    fireEvent.change(screen.getByLabelText(/display name/i), { target: { value: 'John Doe' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'Password1!' } });
    fireEvent.change(screen.getByLabelText(/given name/i), { target: { value: 'John' } });
    fireEvent.change(screen.getByLabelText(/family name/i), { target: { value: 'Doe' } });
    fireEvent.change(screen.getByLabelText(/preferred username/i), { target: { value: 'john.doe' } });
    fireEvent.change(screen.getByLabelText(/profile url/i), { target: { value: 'https://example.com/john' } });
    await user.click(screen.getByRole('button', { name: /create user/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({
        email: 'john@example.com',
        displayName: 'John Doe',
        password: 'Password1!',
        profile: expect.objectContaining({
          givenName: 'John',
          familyName: 'Doe',
          preferredUsername: 'john.doe',
          profile: 'https://example.com/john',
          picture: null,
        }),
      });
    });
  });

  it('prefills edit mode, submits profile updates, and calls cancel', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    const onCancel = vi.fn();
    render(<UserForm user={existingUser} onSubmit={onSubmit} onCancel={onCancel} />);

    expect(screen.queryByLabelText(/email/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/password/i)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/display name/i)).toHaveValue('Jane Existing');
    expect(screen.getByLabelText(/given name/i)).toHaveValue('Jane');
    expect(screen.getByLabelText(/profile url/i)).toHaveValue('https://example.com/jane');

    await user.clear(screen.getByLabelText(/display name/i));
    await user.type(screen.getByLabelText(/display name/i), 'Jane Updated');
    await user.clear(screen.getByLabelText(/nickname/i));
    await user.click(screen.getByRole('button', { name: /update user/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({
        displayName: 'Jane Updated',
        profile: expect.objectContaining({
          givenName: 'Jane',
          nickname: null,
          website: 'https://jane.example.com',
        }),
      });
    });

    await user.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('shows validation and submit errors without calling successful submit', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockRejectedValue({
      title: 'Create failed',
      detail: 'Email is already registered.',
    });
    render(<UserForm onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText(/email/i), 'valid@example.com');
    await user.type(screen.getByLabelText(/display name/i), 'Invalid User');
    await user.type(screen.getByLabelText(/password/i), 'weak');
    await user.type(screen.getByLabelText(/profile url/i), 'not-a-url');
    await user.click(screen.getByRole('button', { name: /create user/i }));

    expect(await screen.findByText('Password must be at least 8 characters')).toBeInTheDocument();
    expect(screen.getByText('Enter a valid http or https URL')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();

    await user.clear(screen.getByLabelText(/email/i));
    await user.type(screen.getByLabelText(/email/i), 'taken@example.com');
    await user.clear(screen.getByLabelText(/password/i));
    await user.type(screen.getByLabelText(/password/i), 'Password1!');
    await user.clear(screen.getByLabelText(/profile url/i));
    await user.click(screen.getByRole('button', { name: /create user/i }));

    expect(await screen.findByText('Email is already registered.')).toBeInTheDocument();
  });
});
