import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useEffect } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { AppRoutes } from '@/routes/AppRoutes';
import { renderManagementWeb } from '@/test/render';
import { mockApi, page, resetApiMocks } from '@/test/mock-api';
import { AuthProvider, useAuth, type AuthContextValue } from './auth';

type MockOidcUser = {
  expired: boolean;
  access_token: string;
  profile: Record<string, unknown>;
};

let storedUser: MockOidcUser | null;
let loadedCallback: ((user: MockOidcUser) => void) | null;
let unloadedCallback: (() => void) | null;
let expiredCallback: (() => void) | null;
let getUser: Mock<() => Promise<MockOidcUser | null>>;
let signoutRedirect: Mock<() => Promise<void>>;
let signinRedirect: Mock<(args?: { prompt: string; max_age: number }) => Promise<void>>;
let removeUser: Mock<() => Promise<void>>;
let latestAuth: AuthContextValue | null;

vi.mock('oidc-client-ts', () => ({
  WebStorageStateStore: vi.fn(),
  UserManager: vi.fn(function UserManagerMock(this: Record<string, unknown>) {
    this.getUser = getUser;
    this.signinRedirect = signinRedirect;
    this.removeUser = removeUser;
    this.signinCallback = vi.fn();
    this.signoutRedirect = signoutRedirect;
    this.events = {
      addUserLoaded: vi.fn((callback: (user: MockOidcUser) => void) => {
        loadedCallback = callback;
        return () => {
          loadedCallback = null;
        };
      }),
      addUserUnloaded: vi.fn((callback: () => void) => {
        unloadedCallback = callback;
        return () => {
          unloadedCallback = null;
        };
      }),
      addAccessTokenExpired: vi.fn((callback: () => void) => {
        expiredCallback = callback;
        return () => {
          expiredCallback = null;
        };
      }),
    };
  }),
}));

vi.mock('@/lib/api', async () => ({
  api: (await import('@/test/mock-api')).mockApi,
  getApiErrorMessage: (error: unknown) => (error instanceof Error ? error.message : String(error)),
  setAccessTokenProvider: vi.fn(),
  setUnauthorizedHandler: vi.fn(),
  setAdministrativeApprovalHandler: vi.fn(),
}));

function AuthProbe() {
  const auth = useAuth();
  useEffect(() => {
    latestAuth = auth;
  });

  return (
    <div>
      <span data-testid="loading">{String(auth.isLoading)}</span>
      <span data-testid="authenticated">{String(auth.isAuthenticated)}</span>
      <span data-testid="display-name">{auth.displayName}</span>
      <span data-testid="permissions">{auth.permissions.join(',')}</span>
    </div>
  );
}

describe('AuthProvider current user permissions', () => {
  beforeEach(() => {
    storedUser = {
      expired: false,
      access_token: 'opaque-token-without-dots',
      profile: { sub: 'user-123', name: 'Token Name' },
    };
    loadedCallback = null;
    unloadedCallback = null;
    expiredCallback = null;
    latestAuth = null;
    getUser = vi.fn(async () => storedUser);
    removeUser = vi.fn(async () => {
      storedUser = null;
      unloadedCallback?.();
    });
    signoutRedirect = vi.fn(async () => {
      await removeUser();
    });
    signinRedirect = vi.fn(async () => {});
    resetApiMocks();
    mockApi.currentUser.getCurrentUser.mockResolvedValue({
      subject: 'user-123',
      userName: 'ada',
      displayName: 'Ada Lovelace',
      email: 'ada@example.com',
      permissions: ['users:read'],
    });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('loads permissions from /api/me for an opaque access token', async () => {
    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'));

    expect(mockApi.currentUser.getCurrentUser).toHaveBeenCalledOnce();
    expect(screen.getByTestId('authenticated')).toHaveTextContent('true');
    expect(screen.getByTestId('display-name')).toHaveTextContent('Ada Lovelace');
    expect(screen.getByTestId('permissions')).toHaveTextContent('users:read');
  });

  it('keeps auth loading until /api/me resolves', async () => {
    let resolveCurrentUser!: (value: Awaited<ReturnType<typeof mockApi.currentUser.getCurrentUser>>) => void;
    mockApi.currentUser.getCurrentUser.mockReturnValue(
      new Promise((resolve) => {
        resolveCurrentUser = resolve;
      })
    );

    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    );

    await waitFor(() => expect(mockApi.currentUser.getCurrentUser).toHaveBeenCalledOnce());
    expect(screen.getByTestId('loading')).toHaveTextContent('true');

    resolveCurrentUser({
      subject: 'user-123',
      userName: 'ada',
      displayName: 'Ada Lovelace',
      email: null,
      permissions: ['roles:read'],
    });

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'));
    expect(screen.getByTestId('permissions')).toHaveTextContent('roles:read');
  });

  it('clears rejected credentials and requires fresh authentication when /api/me returns 401', async () => {
    mockApi.currentUser.getCurrentUser.mockRejectedValue({ status: 401, title: 'Unauthorized' });

    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    );

    await waitFor(() => expect(signinRedirect).toHaveBeenCalledWith({ prompt: 'login', max_age: 0 }));
    expect(removeUser).toHaveBeenCalledOnce();
    expect(signoutRedirect).not.toHaveBeenCalled();
  });

  it('shows an explicit error when /api/me fails without 401', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    mockApi.currentUser.getCurrentUser.mockRejectedValue({ status: 500, title: 'Server Error' });

    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    );

    expect(await screen.findByText('Unable to load your authorization state.')).toBeInTheDocument();
  });

  it('refetches /api/me when oidc-client-ts raises a loaded user with a changed access token', async () => {
    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    );

    await waitFor(() => expect(mockApi.currentUser.getCurrentUser).toHaveBeenCalledTimes(1));

    act(() => {
      loadedCallback?.({
        expired: false,
        access_token: 'new-opaque-token',
        profile: { sub: 'user-123', name: 'Token Name' },
      });
    });

    await waitFor(() => expect(mockApi.currentUser.getCurrentUser).toHaveBeenCalledTimes(2));
  });

  it('keeps the open page mounted when silent renew replaces the access token', async () => {
    mockApi.users.getUsers.mockResolvedValue(page([]));
    const user = userEvent.setup();
    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
        <AppRoutes />
      </AuthProvider>,
      { initialEntries: ['/users'] }
    );

    const search = await screen.findByRole('textbox', { name: 'Search users' });
    await user.type(search, 'ada');
    expect(screen.getByTestId('loading')).toHaveTextContent('false');

    let resolveRenewedUser!: (value: CurrentUser) => void;
    mockApi.currentUser.getCurrentUser.mockReturnValue(
      new Promise((resolve) => {
        resolveRenewedUser = resolve;
      })
    );

    act(() => {
      loadedCallback?.({
        expired: false,
        access_token: 'renewed-token',
        profile: { sub: 'user-123', name: 'Token Name' },
      });
    });

    expect(mockApi.currentUser.getCurrentUser).toHaveBeenCalledTimes(2);
    expect(screen.queryByText('Loading console…')).not.toBeInTheDocument();
    expect(screen.getByTestId('loading')).toHaveTextContent('false');
    expect(screen.getByTestId('permissions')).toHaveTextContent('users:read');
    expect(screen.getByTestId('display-name')).toHaveTextContent('Ada Lovelace');
    expect(screen.getByRole('textbox', { name: 'Search users' })).toHaveValue('ada');

    resolveRenewedUser(operator(['users:read']));

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'));
    expect(screen.getByRole('textbox', { name: 'Search users' })).toHaveValue('ada');
    expect(screen.queryByText('Loading console…')).not.toBeInTheDocument();
  });

  it('returns the in-memory access token without reading the user store on each call', async () => {
    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'));
    const storeReads = getUser.mock.calls.length;

    await expect(latestAuth?.getAccessToken()).resolves.toBe('opaque-token-without-dots');
    await expect(latestAuth?.getAccessToken()).resolves.toBe('opaque-token-without-dots');
    expect(getUser).toHaveBeenCalledTimes(storeReads);

    await act(async () => {
      loadedCallback?.({
        expired: false,
        access_token: 'renewed-token',
        profile: { sub: 'user-123', name: 'Token Name' },
      });
    });

    await expect(latestAuth?.getAccessToken()).resolves.toBe('renewed-token');
    expect(getUser).toHaveBeenCalledTimes(storeReads);
  });

  it('drops the previous operator while a different subject is revalidated', async () => {
    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('permissions')).toHaveTextContent('users:read'));

    let resolveSwitchedUser!: (value: CurrentUser) => void;
    mockApi.currentUser.getCurrentUser.mockReturnValue(
      new Promise((resolve) => {
        resolveSwitchedUser = resolve;
      })
    );

    act(() => {
      loadedCallback?.({
        expired: false,
        access_token: 'switched-token',
        profile: { sub: 'user-999', name: 'Grace Hopper' },
      });
    });

    expect(screen.getByTestId('loading')).toHaveTextContent('true');
    expect(screen.getByTestId('permissions')).toHaveTextContent('');
    expect(screen.getByTestId('display-name')).toHaveTextContent('Grace Hopper');
    expect(screen.getByTestId('display-name')).not.toHaveTextContent('Ada Lovelace');

    resolveSwitchedUser({
      subject: 'user-999',
      userName: 'grace',
      displayName: 'Grace Hopper',
      email: null,
      permissions: ['roles:read'],
    });

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'));
    expect(screen.getByTestId('permissions')).toHaveTextContent('roles:read');
    expect(screen.getByTestId('display-name')).toHaveTextContent('Grace Hopper');
  });

  it('signs out when the user manager unloads the session', async () => {
    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('authenticated')).toHaveTextContent('true'));

    await act(async () => {
      await latestAuth?.logout();
    });

    expect(signoutRedirect).toHaveBeenCalledOnce();
    expect(screen.getByTestId('authenticated')).toHaveTextContent('false');
    expect(screen.getByTestId('loading')).toHaveTextContent('false');
    expect(screen.getByTestId('permissions')).toHaveTextContent('');
    await expect(latestAuth?.getAccessToken()).resolves.toBeNull();
    expect(getUser).toHaveBeenCalledOnce();
  });

  it('returns no access token once the cached session expires', async () => {
    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('authenticated')).toHaveTextContent('true'));
    const storeReads = getUser.mock.calls.length;
    storedUser!.expired = true;

    await expect(latestAuth?.getAccessToken()).resolves.toBeNull();
    expect(getUser).toHaveBeenCalledTimes(storeReads);

    act(() => {
      expiredCallback?.();
    });

    expect(screen.getByTestId('authenticated')).toHaveTextContent('false');
    expect(screen.getByTestId('loading')).toHaveTextContent('false');
    expect(screen.getByTestId('permissions')).toHaveTextContent('');
  });

  it('does not authenticate an already expired session', async () => {
    storedUser = {
      expired: true,
      access_token: 'stale-token',
      profile: { sub: 'user-123', name: 'Token Name' },
    };

    renderManagementWeb(
      <AuthProvider>
        <AuthProbe />
      </AuthProvider>
    );

    await waitFor(() => expect(screen.getByTestId('loading')).toHaveTextContent('false'));
    expect(screen.getByTestId('authenticated')).toHaveTextContent('false');
    expect(mockApi.currentUser.getCurrentUser).not.toHaveBeenCalled();
    await expect(latestAuth?.getAccessToken()).resolves.toBeNull();
  });
});

type CurrentUser = {
  subject: string;
  userName: string;
  displayName: string;
  email: string | null;
  permissions: string[];
};

function operator(permissions: string[]): CurrentUser {
  return {
    subject: 'user-123',
    userName: 'ada',
    displayName: 'Ada Lovelace',
    email: 'ada@example.com',
    permissions,
  };
}
