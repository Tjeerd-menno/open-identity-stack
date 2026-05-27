import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

vi.mock('../services/oidc-config', () => ({
  oidcConfig: {},
  extractPermissions: vi.fn(() => []),
  extractDisplayName: vi.fn(() => 'Mock User'),
}));

import { AuthContext, type AuthContextValue } from '../auth-context';
import { SilentCallback } from './SilentCallback';

const mockAuthValue = (overrides: Partial<AuthContextValue> = {}): AuthContextValue => ({
  isAuthenticated: false,
  isLoading: false,
  isLoggingOut: false,
  user: null,
  accessToken: null,
  error: null,
  login: vi.fn(),
  logout: vi.fn(),
  signinCallback: vi.fn(),
  signinSilentCallback: vi.fn().mockResolvedValue(undefined),
  getAccessToken: vi.fn().mockResolvedValue(null),
  ...overrides,
});

const renderWithAuth = (value: Partial<AuthContextValue> = {}) =>
  render(
    <AuthContext.Provider value={mockAuthValue(value)}>
      <SilentCallback />
    </AuthContext.Provider>
  );

describe('SilentCallback', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('calls signinSilentCallback on mount and renders a hidden container', async () => {
    const signinSilentCallback = vi.fn().mockResolvedValue(undefined);

    renderWithAuth({ signinSilentCallback });

    await waitFor(() => {
      expect(signinSilentCallback).toHaveBeenCalledOnce();
    });

    expect(screen.getByText(/processing silent authentication/i).parentElement).toHaveStyle({
      display: 'none',
    });
  });

  it('logs silent callback errors without showing a visible error UI', async () => {
    const error = new Error('Silent renewal failed');
    const signinSilentCallback = vi.fn().mockRejectedValue(error);
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});

    renderWithAuth({ signinSilentCallback });

    await waitFor(() => {
      expect(consoleError).toHaveBeenCalledWith('Silent callback error:', error);
    });
    expect(screen.getByText(/processing silent authentication/i)).toBeInTheDocument();
  });
});
