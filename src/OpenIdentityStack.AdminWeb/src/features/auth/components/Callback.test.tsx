import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../services/oidc-config', () => ({
  oidcConfig: {},
  extractPermissions: vi.fn(() => []),
  extractDisplayName: vi.fn(() => 'Mock User'),
}));

import { AuthContext, type AuthContextValue } from '../AuthContext';
import { Callback } from './Callback';

const mockNavigate = vi.fn();

vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}));

const mockAuthValue = (overrides: Partial<AuthContextValue> = {}): AuthContextValue => ({
  isAuthenticated: false,
  isLoading: false,
  isLoggingOut: false,
  user: null,
  accessToken: null,
  error: null,
  login: vi.fn(),
  logout: vi.fn(),
  signinCallback: vi.fn().mockResolvedValue(undefined),
  signinSilentCallback: vi.fn(),
  getAccessToken: vi.fn().mockResolvedValue(null),
  ...overrides,
});

const renderWithAuth = (value: Partial<AuthContextValue> = {}, strictMode = false) => {
  const tree = (
    <AuthContext.Provider value={mockAuthValue(value)}>
      <Callback />
    </AuthContext.Provider>
  );

  return render(strictMode ? <React.StrictMode>{tree}</React.StrictMode> : tree);
};

describe('Callback', () => {
  beforeEach(() => {
    mockNavigate.mockReset();
  });

  it('calls signinCallback and then navigates home', async () => {
    const signinCallback = vi.fn().mockResolvedValue(undefined);

    renderWithAuth({ signinCallback });

    await waitFor(() => {
      expect(signinCallback).toHaveBeenCalledOnce();
      expect(mockNavigate).toHaveBeenCalledWith('/', { replace: true });
    });
  });

  it('shows an error state when signinCallback fails and lets the user go back to sign in', async () => {
    const user = userEvent.setup();
    const signinCallback = vi.fn().mockRejectedValue(new Error('Authorization code is invalid'));

    renderWithAuth({ signinCallback });

    expect(await screen.findByText('Sign-In Failed')).toBeInTheDocument();
    expect(screen.getByText('Authorization code is invalid')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /back to sign in/i }));

    expect(mockNavigate).toHaveBeenCalledWith('/login');
  });

  it('guards against double execution in strict mode', async () => {
    const signinCallback = vi.fn().mockResolvedValue(undefined);

    renderWithAuth({ signinCallback }, true);

    await waitFor(() => {
      expect(signinCallback).toHaveBeenCalledTimes(1);
    });
  });
});
