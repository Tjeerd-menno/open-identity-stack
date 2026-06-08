import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

vi.mock('../services/oidc-config', () => ({
  oidcConfig: {},
  extractPermissions: vi.fn(() => []),
  extractDisplayName: vi.fn(() => 'Mock User'),
}));

import { AuthContext, type AuthContextValue } from '../auth-context';
import { useAuthorization, useRequireAuth } from './useRequireAuth';

const mockNavigate = vi.fn();
const mockUseLocation = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
    useLocation: () => mockUseLocation(),
  };
});

const mockAuthValue = (overrides: Partial<AuthContextValue> = {}): AuthContextValue => ({
  isAuthenticated: true,
  isLoading: false,
  isLoggingOut: false,
  user: { sub: 'u1', email: 'alice@example.com', name: 'Alice', permissions: ['users:read'] },
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

function RequireAuthProbe({ requiredPermissions }: { requiredPermissions?: string[] }) {
  const auth = useRequireAuth(requiredPermissions);

  return (
    <div>
      <span data-testid="is-loading">{String(auth.isLoading)}</span>
      <span data-testid="is-authenticated">{String(auth.isAuthenticated)}</span>
      <span data-testid="user-email">{auth.user?.email ?? 'none'}</span>
    </div>
  );
}

function AuthorizationProbe({ requiredPermissions }: { requiredPermissions: string[] }) {
  const state = useAuthorization(requiredPermissions);

  return (
    <div>
      <span data-testid="is-authorized">{String(state.isAuthorized)}</span>
      <span data-testid="is-loading">{String(state.isLoading)}</span>
    </div>
  );
}

describe('useRequireAuth', () => {
  beforeEach(() => {
    mockNavigate.mockReset();
    mockUseLocation.mockReset();
    mockUseLocation.mockReturnValue({ pathname: '/users/secure', state: null });
  });

  it('waits while auth is loading without redirecting or logging in', async () => {
    const login = vi.fn();

    renderWithAuth(<RequireAuthProbe />, {
      isLoading: true,
      isAuthenticated: false,
      user: null,
      accessToken: null,
      login,
    });

    expect(screen.getByTestId('is-loading')).toHaveTextContent('true');

    await waitFor(() => {
      expect(login).not.toHaveBeenCalled();
      expect(mockNavigate).not.toHaveBeenCalled();
    });
  });

  it('calls login for unauthenticated users', async () => {
    const login = vi.fn().mockResolvedValue(undefined);

    renderWithAuth(<RequireAuthProbe />, {
      isLoading: false,
      isAuthenticated: false,
      user: null,
      accessToken: null,
      login,
    });

    await waitFor(() => {
      expect(login).toHaveBeenCalledOnce();
    });
    expect(mockNavigate).not.toHaveBeenCalled();
  });

  it('redirects authenticated users without required permissions to access denied', async () => {
    renderWithAuth(<RequireAuthProbe requiredPermissions={['users:read', 'roles:update']} />, {
      user: { sub: 'u1', email: 'alice@example.com', name: 'Alice', permissions: ['users:read'] },
    });

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/access-denied', {
        state: { from: { pathname: '/users/secure', state: null } },
        replace: true,
      });
    });
  });

  it('does not redirect when required permissions are present', async () => {
    const login = vi.fn();

    renderWithAuth(<RequireAuthProbe requiredPermissions={['users:read', 'roles:update']} />, {
      login,
      user: {
        sub: 'u1',
        email: 'alice@example.com',
        name: 'Alice',
        permissions: ['users:read', 'roles:update'],
      },
    });

    expect(screen.getByTestId('is-authenticated')).toHaveTextContent('true');
    expect(screen.getByTestId('user-email')).toHaveTextContent('alice@example.com');

    await waitFor(() => {
      expect(mockNavigate).not.toHaveBeenCalled();
      expect(login).not.toHaveBeenCalled();
    });
  });

  it('does not redirect when wildcard permissions satisfy route requirements', async () => {
    renderWithAuth(<RequireAuthProbe requiredPermissions={['application-permissions:read']} />, {
      user: {
        sub: 'u1',
        email: 'alice@example.com',
        name: 'Alice',
        permissions: ['application-permissions:*'],
      },
    });

    await waitFor(() => {
      expect(mockNavigate).not.toHaveBeenCalled();
    });
  });
});

describe('useAuthorization', () => {
  it('returns loading state while auth is loading', () => {
    renderWithAuth(<AuthorizationProbe requiredPermissions={['users:read']} />, {
      isLoading: true,
      isAuthenticated: false,
      user: null,
      accessToken: null,
    });

    expect(screen.getByTestId('is-authorized')).toHaveTextContent('false');
    expect(screen.getByTestId('is-loading')).toHaveTextContent('true');
  });

  it('returns unauthorized when unauthenticated', () => {
    renderWithAuth(<AuthorizationProbe requiredPermissions={['users:read']} />, {
      isAuthenticated: false,
      isLoading: false,
      user: null,
      accessToken: null,
    });

    expect(screen.getByTestId('is-authorized')).toHaveTextContent('false');
    expect(screen.getByTestId('is-loading')).toHaveTextContent('false');
  });

  it('returns authorized only when all required permissions are present', () => {
    const { rerender } = renderWithAuth(<AuthorizationProbe requiredPermissions={['users:read', 'roles:update']} />, {
      user: {
        sub: 'u1',
        email: 'alice@example.com',
        name: 'Alice',
        permissions: ['users:read', 'roles:update'],
      },
    });

    expect(screen.getByTestId('is-authorized')).toHaveTextContent('true');
    expect(screen.getByTestId('is-loading')).toHaveTextContent('false');

    rerender(
      <AuthContext.Provider
        value={mockAuthValue({
          user: {
            sub: 'u1',
            email: 'alice@example.com',
            name: 'Alice',
            permissions: ['users:read'],
          },
        })}
      >
        <AuthorizationProbe requiredPermissions={['users:read', 'roles:update']} />
      </AuthContext.Provider>
    );

    expect(screen.getByTestId('is-authorized')).toHaveTextContent('false');
  });

  it('returns authorized when wildcard grants satisfy required permissions', () => {
    renderWithAuth(<AuthorizationProbe requiredPermissions={['users:read', 'users:update']} />, {
      user: {
        sub: 'u1',
        email: 'alice@example.com',
        name: 'Alice',
        permissions: ['users:*'],
      },
    });

    expect(screen.getByTestId('is-authorized')).toHaveTextContent('true');
  });
});
