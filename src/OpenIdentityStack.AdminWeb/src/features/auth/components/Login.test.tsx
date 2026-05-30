import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../services/oidc-config', () => ({
  oidcConfig: {},
  extractPermissions: vi.fn(() => []),
  extractDisplayName: vi.fn(() => 'Mock User'),
}));

import { AuthContext, type AuthContextValue } from '../AuthContext';
import { Login } from './Login';

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
  login: vi.fn().mockResolvedValue(undefined),
  logout: vi.fn(),
  signinCallback: vi.fn(),
  signinSilentCallback: vi.fn(),
  getAccessToken: vi.fn().mockResolvedValue(null),
  ...overrides,
});

const renderWithAuth = (value: Partial<AuthContextValue> = {}) =>
  render(
    <AuthContext.Provider value={mockAuthValue(value)}>
      <Login />
    </AuthContext.Provider>
  );

describe('Login', () => {
  beforeEach(() => {
    mockNavigate.mockReset();
  });

  it('navigates to the home page when the user is already authenticated', async () => {
    const login = vi.fn();

    renderWithAuth({ isAuthenticated: true, login });

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/', { replace: true });
    });
    expect(login).not.toHaveBeenCalled();
  });

  it('starts the login flow when the user is not authenticated and not loading', async () => {
    const login = vi.fn().mockResolvedValue(undefined);

    renderWithAuth({ login });

    await waitFor(() => {
      expect(login).toHaveBeenCalledOnce();
    });
    expect(mockNavigate).not.toHaveBeenCalled();
    expect(screen.getByText(/redirecting to sign-in/i)).toBeInTheDocument();
  });

  it('shows an authentication error from auth state', () => {
    renderWithAuth({ error: 'OIDC configuration is invalid.' });

    expect(screen.getByText('OIDC configuration is invalid.')).toBeInTheDocument();
  });
});
