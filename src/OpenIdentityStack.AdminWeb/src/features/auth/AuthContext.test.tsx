import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const renderAuthSnapshot = async () => {
  const { AuthContext } = await import('./AuthContext');

  const AuthSnapshot = () => {
    const auth = React.useContext(AuthContext);

    if (!auth) {
      return null;
    }

    return (
      <div>
        <span data-testid="authenticated">{String(auth.isAuthenticated)}</span>
        <span data-testid="loading">{String(auth.isLoading)}</span>
        <span data-testid="email">{auth.user?.email ?? 'none'}</span>
        <span data-testid="name">{auth.user?.name ?? 'none'}</span>
        <span data-testid="token">{auth.accessToken ?? 'none'}</span>
      </div>
    );
  };

  return { AuthSnapshot };
};

describe('AuthProvider', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.unstubAllEnvs();
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.clearAllMocks();
    vi.doUnmock('oidc-client-ts');
    vi.doUnmock('./services/oidc-config');
    vi.doUnmock('@/lib/api/client');
  });

  it('uses MockAuthProvider values when E2E test mode is enabled', async () => {
    vi.stubEnv('VITE_E2E_TEST_MODE', 'true');

    const { AuthProvider } = await import('./AuthContext');
    const { AuthSnapshot } = await renderAuthSnapshot();

    render(
      <AuthProvider>
        <AuthSnapshot />
      </AuthProvider>
    );

    expect(screen.getByTestId('authenticated')).toHaveTextContent('true');
    expect(screen.getByTestId('loading')).toHaveTextContent('false');
    expect(screen.getByTestId('email')).toHaveTextContent('admin@example.com');
    expect(screen.getByTestId('name')).toHaveTextContent('E2E Test Admin');
    expect(screen.getByTestId('token')).toHaveTextContent('e2e-test-token');
  });

  it('initializes OIDC auth state on mount from UserManager', async () => {
    vi.stubEnv('VITE_E2E_TEST_MODE', 'false');

    const mockUserManager = {
      getUser: vi.fn().mockResolvedValue({
        expired: false,
        access_token: 'oidc-token',
        profile: {
          sub: 'u1',
          email: 'alice@example.com',
        },
      }),
      removeUser: vi.fn(),
      signinRedirect: vi.fn(),
      signinRedirectCallback: vi.fn(),
      signinSilentCallback: vi.fn(),
      settings: {
        authority: 'https://identity.example.com',
        post_logout_redirect_uri: 'http://localhost:5173/',
        metadata: { end_session_endpoint: 'https://identity.example.com/connect/logout' },
      },
      events: {
        addUserLoaded: vi.fn(),
        addUserUnloaded: vi.fn(),
        addSilentRenewError: vi.fn(),
        addAccessTokenExpiring: vi.fn(),
        addAccessTokenExpired: vi.fn(),
        removeUserLoaded: vi.fn(),
        removeUserUnloaded: vi.fn(),
        removeSilentRenewError: vi.fn(),
        removeAccessTokenExpiring: vi.fn(),
        removeAccessTokenExpired: vi.fn(),
      },
    };

    const setTokenProvider = vi.fn();
    const setLogoutHandler = vi.fn();

    class MockUserManager {
      constructor() {
        return mockUserManager as never;
      }
    }

    vi.doMock('oidc-client-ts', () => ({
      UserManager: MockUserManager,
      WebStorageStateStore: vi.fn(() => ({})),
      User: class {},
    }));

    vi.doMock('./services/oidc-config', () => ({
      oidcConfig: { authority: 'https://identity.example.com' },
      extractPermissions: vi.fn(() => ['users:read', 'roles:read']),
      extractDisplayName: vi.fn(() => 'Alice Example'),
    }));

    vi.doMock('@/lib/api/client', () => ({
      apiClient: {
        setTokenProvider,
        setLogoutHandler,
      },
    }));

    const { AuthProvider } = await import('./AuthContext');
    const { AuthSnapshot } = await renderAuthSnapshot();

    render(
      <AuthProvider>
        <AuthSnapshot />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByTestId('authenticated')).toHaveTextContent('true');
      expect(screen.getByTestId('loading')).toHaveTextContent('false');
      expect(screen.getByTestId('email')).toHaveTextContent('alice@example.com');
      expect(screen.getByTestId('name')).toHaveTextContent('Alice Example');
      expect(screen.getByTestId('token')).toHaveTextContent('oidc-token');
    });

    expect(mockUserManager.getUser).toHaveBeenCalled();
    expect(setTokenProvider).toHaveBeenCalled();
    expect(setLogoutHandler).toHaveBeenCalled();
    expect(mockUserManager.events.addUserLoaded).toHaveBeenCalled();
    expect(mockUserManager.events.addAccessTokenExpired).toHaveBeenCalled();
  });
});
