import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../services/oidc-config', () => ({
  oidcConfig: {},
  extractPermissions: vi.fn(() => []),
  extractDisplayName: vi.fn(() => 'Mock User'),
}));

import { AuthContext, type AuthContextValue } from '../AuthContext';
import { AccessDenied, ProtectedRoute } from './ProtectedRoute';

const mockUseLocation = vi.fn();

vi.mock('react-router-dom', () => ({
  Navigate: ({ to, replace }: { to: string; replace?: boolean }) => (
    <span data-testid="navigate" data-replace={String(Boolean(replace))} data-to={to} />
  ),
  useLocation: () => mockUseLocation(),
}));

const mockAuthValue = (overrides: Partial<AuthContextValue> = {}): AuthContextValue => ({
  isAuthenticated: true,
  isLoading: false,
  isLoggingOut: false,
  user: { sub: 'u1', email: 'a@b.com', name: 'Alice', permissions: ['users:read'] },
  accessToken: 'tok',
  error: null,
  login: vi.fn(),
  logout: vi.fn(),
  signinCallback: vi.fn(),
  signinSilentCallback: vi.fn(),
  getAccessToken: vi.fn().mockResolvedValue('tok'),
  ...overrides,
});

const renderWithAuth = (ui: React.ReactNode, value: Partial<AuthContextValue> = {}) =>
  render(<AuthContext.Provider value={mockAuthValue(value)}>{ui}</AuthContext.Provider>);

describe('ProtectedRoute', () => {
  beforeEach(() => {
    mockUseLocation.mockReturnValue({ pathname: '/protected', state: null });
  });

  it('shows a loading spinner while auth state is loading', () => {
    const { container } = renderWithAuth(
      <ProtectedRoute>
        <div>Protected content</div>
      </ProtectedRoute>,
      { isLoading: true }
    );

    expect(container.querySelector('.animate-spin')).toBeInTheDocument();
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
  });

  it('shows a loading spinner while logout is in progress', () => {
    const { container } = renderWithAuth(
      <ProtectedRoute>
        <div>Protected content</div>
      </ProtectedRoute>,
      { isLoggingOut: true }
    );

    expect(container.querySelector('.animate-spin')).toBeInTheDocument();
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
  });

  it('redirects unauthenticated users to login', () => {
    renderWithAuth(
      <ProtectedRoute>
        <div>Protected content</div>
      </ProtectedRoute>,
      { isAuthenticated: false, user: null, accessToken: null }
    );

    expect(screen.getByTestId('navigate')).toHaveAttribute('data-to', '/login');
    expect(screen.getByTestId('navigate')).toHaveAttribute('data-replace', 'true');
  });

  it('renders children for authenticated users', () => {
    renderWithAuth(
      <ProtectedRoute>
        <div>Protected content</div>
      </ProtectedRoute>
    );

    expect(screen.getByText('Protected content')).toBeInTheDocument();
  });

  it('redirects to access denied when requireAnyPermission is set and the user has no permissions', () => {
    renderWithAuth(
      <ProtectedRoute requireAnyPermission>
        <div>Protected content</div>
      </ProtectedRoute>,
      {
        user: { sub: 'u1', email: 'a@b.com', name: 'Alice', permissions: [] },
      }
    );

    expect(screen.getByTestId('navigate')).toHaveAttribute('data-to', '/access-denied');
  });

  it('redirects to access denied when required permissions are missing', () => {
    renderWithAuth(
      <ProtectedRoute requiredPermissions={['users:read', 'roles:update']}>
        <div>Protected content</div>
      </ProtectedRoute>,
      {
        user: { sub: 'u1', email: 'a@b.com', name: 'Alice', permissions: ['users:read'] },
      }
    );

    expect(screen.getByTestId('navigate')).toHaveAttribute('data-to', '/access-denied');
  });

  it('renders children when required permissions are satisfied', () => {
    renderWithAuth(
      <ProtectedRoute requiredPermissions={['users:read', 'roles:update']}>
        <div>Protected content</div>
      </ProtectedRoute>,
      {
        user: {
          sub: 'u1',
          email: 'a@b.com',
          name: 'Alice',
          permissions: ['users:read', 'roles:update'],
        },
      }
    );

    expect(screen.getByText('Protected content')).toBeInTheDocument();
  });
});

describe('AccessDenied', () => {
  beforeEach(() => {
    mockUseLocation.mockReturnValue({
      pathname: '/access-denied',
      state: { from: { pathname: '/users/secret' } },
    });
  });

  it('shows the access denied page and signs out when requested', async () => {
    const user = userEvent.setup();
    const logout = vi.fn().mockResolvedValue(undefined);

    renderWithAuth(<AccessDenied />, { logout });

    expect(screen.getByText('Access Denied')).toBeInTheDocument();
    expect(screen.getByText("You don't have permission to access this page.")).toBeInTheDocument();
    expect(screen.getByText('/users/secret')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /sign out/i }));

    expect(logout).toHaveBeenCalledOnce();
  });
});
