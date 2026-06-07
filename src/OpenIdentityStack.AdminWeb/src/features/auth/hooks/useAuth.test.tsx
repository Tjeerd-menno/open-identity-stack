import React from 'react';
import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

vi.mock('../services/oidc-config', () => ({
  oidcConfig: {},
  extractPermissions: vi.fn(() => []),
  extractDisplayName: vi.fn(() => 'Mock User'),
}));

import { AuthContext, type AuthContextValue } from '../auth-context';
import { useAllPermissions, useAuth, usePermission, usePermissions } from './useAuth';

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

const makeAuthWrapper = (value: Partial<AuthContextValue> = {}) => {
  const fullValue = mockAuthValue(value);

  return ({ children }: { children: React.ReactNode }) => (
    <AuthContext.Provider value={fullValue}>{children}</AuthContext.Provider>
  );
};

describe('useAuth hooks', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('throws when useAuth is used outside AuthProvider', () => {
    const OutsideProvider = () => {
      useAuth();
      return null;
    };

    expect(() => render(<OutsideProvider />)).toThrow('useAuth must be used within an AuthProvider');
  });

  it('returns the auth context value from the provider', () => {
    const wrapper = makeAuthWrapper({
      user: { sub: 'u1', email: 'alice@example.com', name: 'Alice', permissions: ['users:read'] },
      accessToken: 'access-token',
    });

    const AuthConsumer = () => {
      const auth = useAuth();

      return (
        <div>
          <span data-testid="email">{auth.user?.email}</span>
          <span data-testid="token">{auth.accessToken}</span>
          <span data-testid="authenticated">{String(auth.isAuthenticated)}</span>
        </div>
      );
    };

    render(<AuthConsumer />, { wrapper });

    expect(screen.getByTestId('email')).toHaveTextContent('alice@example.com');
    expect(screen.getByTestId('token')).toHaveTextContent('access-token');
    expect(screen.getByTestId('authenticated')).toHaveTextContent('true');
  });

  it('evaluates permission helper hooks from the authenticated user permissions', () => {
    const wrapper = makeAuthWrapper({
      user: {
        sub: 'u1',
        email: 'alice@example.com',
        name: 'Alice',
        permissions: ['users:*', 'roles:update'],
      },
    });

    const PermissionConsumer = () => {
      const canReadUsers = usePermission('users:read');
      const canCreateUsers = usePermission('users:create');
      const canManageAny = usePermissions(['groups:read', 'roles:update']);
      const canManageGroups = usePermissions(['groups:update']);
      const hasAll = useAllPermissions(['users:read', 'roles:update']);
      const missingGroupPermission = useAllPermissions(['users:read', 'groups:update']);

      return (
        <div>
          <span data-testid="can-read-users">{String(canReadUsers)}</span>
          <span data-testid="can-create-users">{String(canCreateUsers)}</span>
          <span data-testid="can-manage-any">{String(canManageAny)}</span>
          <span data-testid="can-manage-groups">{String(canManageGroups)}</span>
          <span data-testid="has-all">{String(hasAll)}</span>
          <span data-testid="missing-group-permission">{String(missingGroupPermission)}</span>
        </div>
      );
    };

    render(<PermissionConsumer />, { wrapper });

    expect(screen.getByTestId('can-read-users')).toHaveTextContent('true');
    expect(screen.getByTestId('can-create-users')).toHaveTextContent('true');
    expect(screen.getByTestId('can-manage-any')).toHaveTextContent('true');
    expect(screen.getByTestId('can-manage-groups')).toHaveTextContent('false');
    expect(screen.getByTestId('has-all')).toHaveTextContent('true');
    expect(screen.getByTestId('missing-group-permission')).toHaveTextContent('false');
  });

  it('returns false from permission hooks when the user is not authenticated', () => {
    const wrapper = makeAuthWrapper({
      isAuthenticated: false,
      user: null,
      accessToken: null,
    });

    const PermissionConsumer = () => {
      const canReadUsers = usePermission('users:read');
      const hasAny = usePermissions(['users:read', 'roles:read']);
      const hasAll = useAllPermissions(['users:read', 'roles:read']);

      return (
        <div>
          <span data-testid="can-read-users">{String(canReadUsers)}</span>
          <span data-testid="has-any">{String(hasAny)}</span>
          <span data-testid="has-all">{String(hasAll)}</span>
        </div>
      );
    };

    render(<PermissionConsumer />, { wrapper });

    expect(screen.getByTestId('can-read-users')).toHaveTextContent('false');
    expect(screen.getByTestId('has-any')).toHaveTextContent('false');
    expect(screen.getByTestId('has-all')).toHaveTextContent('false');
  });
});
