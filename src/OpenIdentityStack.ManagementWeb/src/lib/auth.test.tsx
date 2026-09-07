import { act, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthProvider, useAuth } from './auth';
import { renderManagementWeb } from '@/test/render';
import { mockApi, resetApiMocks } from '@/test/mock-api';

type MockOidcUser = {
  expired: boolean;
  access_token: string;
  profile: Record<string, unknown>;
};

let storedUser: MockOidcUser | null;
let loadedCallback: ((user: MockOidcUser) => void) | null;
let signoutRedirect: ReturnType<typeof vi.fn>;
let signinRedirect: ReturnType<typeof vi.fn>;
let removeUser: ReturnType<typeof vi.fn>;

vi.mock('oidc-client-ts', () => ({
  WebStorageStateStore: vi.fn(),
  UserManager: vi.fn(function UserManagerMock(this: Record<string, unknown>) {
    this.getUser = vi.fn(async () => storedUser);
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
    };
  }),
}));

vi.mock('@/lib/api', async () => ({
  api: (await import('@/test/mock-api')).mockApi,
  setAccessTokenProvider: vi.fn(),
  setUnauthorizedHandler: vi.fn(),
  setAdministrativeApprovalHandler: vi.fn(),
}));

function AuthProbe() {
  const auth = useAuth();

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
      profile: { name: 'Token Name' },
    };
    loadedCallback = null;
    signoutRedirect = vi.fn(async () => {});
    signinRedirect = vi.fn(async () => {});
    removeUser = vi.fn(async () => { storedUser = null; });
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
        profile: { name: 'Token Name' },
      });
    });

    await waitFor(() => expect(mockApi.currentUser.getCurrentUser).toHaveBeenCalledTimes(2));
  });
});
