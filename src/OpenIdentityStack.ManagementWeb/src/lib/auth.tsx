/* eslint-disable react-refresh/only-export-components */
import { UserManager, WebStorageStateStore, type User, type UserManagerSettings } from 'oidc-client-ts';
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useLocation } from 'react-router';
import { setAccessTokenProvider } from './admin-api';

type AuthContextValue = {
  isAuthenticated: boolean;
  isLoading: boolean;
  displayName: string;
  permissions: string[];
  login: () => Promise<void>;
  logout: () => Promise<void>;
  getAccessToken: () => Promise<string | null>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

function isE2ETestMode(): boolean {
  return __E2E_TEST_MODE__ || (import.meta.env.DEV && globalThis.window?.__OIS_E2E_AUTH__ === true);
}

function getOidcAuthority(): string {
  return import.meta.env.VITE_OIDC_AUTHORITY ?? 'http://localhost:5000';
}

function createUserManager(): UserManager {
  const baseUrl = globalThis.location.origin;
  const settings: UserManagerSettings = {
    authority: getOidcAuthority(),
    client_id: import.meta.env.VITE_OIDC_CLIENT_ID ?? 'management-web-client',
    redirect_uri: `${baseUrl}/auth/callback`,
    post_logout_redirect_uri: `${baseUrl}/`,
    silent_redirect_uri: `${baseUrl}/auth/silent-callback`,
    response_type: 'code',
    scope: 'openid profile email api',
    automaticSilentRenew: true,
    userStore: new WebStorageStateStore({ store: globalThis.sessionStorage }),
  };

  return new UserManager(settings);
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }

  return context;
}

type AuthProviderProps = {
  children: ReactNode;
};

export function AuthProvider({ children }: AuthProviderProps) {
  return isE2ETestMode() ? <MockAuthProvider>{children}</MockAuthProvider> : <OidcAuthProvider>{children}</OidcAuthProvider>;
}

function MockAuthProvider({ children }: AuthProviderProps) {
  const value = useMemo<AuthContextValue>(
    () => ({
      isAuthenticated: true,
      isLoading: false,
      displayName: 'E2E Test Admin',
      permissions: ['*'],
      login: async () => {},
      logout: async () => {},
      getAccessToken: async () => 'e2e-test-token',
    }),
    []
  );

  useEffect(() => {
    setAccessTokenProvider(value.getAccessToken);
  }, [value]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function OidcAuthProvider({ children }: AuthProviderProps) {
  const [userManager] = useState(() => createUserManager());
  const [oidcUser, setOidcUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const location = useLocation();

  const getAccessToken = useCallback(async () => {
    const user = await userManager.getUser();
    return user?.expired ? null : user?.access_token ?? null;
  }, [userManager]);

  useEffect(() => {
    let cancelled = false;

    async function processAuthCallback() {
      // Check if we're on the callback route
      if (location.pathname === '/auth/callback') {
        try {
          const user = await userManager.signinCallback();
          if (!cancelled) {
            setOidcUser(user && !user.expired ? user : null);
            setIsLoading(false);
            // Redirect to home after successful login
            window.location.href = '/';
          }
        } catch (error) {
          console.error('OIDC callback error:', error);
          if (!cancelled) {
            setOidcUser(null);
            setIsLoading(false);
            window.location.href = '/';
          }
        }
      } else {
        // Normal initialization: try to get existing user
        try {
          const user = await userManager.getUser();
          if (!cancelled) {
            setOidcUser(user && !user.expired ? user : null);
            setIsLoading(false);
          }
        } catch (error) {
          console.error('Failed to get user:', error);
          if (!cancelled) {
            setOidcUser(null);
            setIsLoading(false);
          }
        }
      }
    }

    processAuthCallback();

    return () => {
      cancelled = true;
    };
  }, [userManager, location.pathname]);

  useEffect(() => {
    setAccessTokenProvider(getAccessToken);
  }, [getAccessToken]);

  const value = useMemo<AuthContextValue>(
    () => ({
      isAuthenticated: !!oidcUser,
      isLoading,
      displayName: typeof oidcUser?.profile.name === 'string' ? oidcUser.profile.name : 'Operator',
      permissions: extractPermissions(oidcUser),
      login: async () => userManager.signinRedirect(),
      logout: async () => userManager.signoutRedirect(),
      getAccessToken,
    }),
    [getAccessToken, isLoading, oidcUser, userManager]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function extractPermissions(user: User | null): string[] {
  if (!user) {
    return [];
  }

  const permissions = new Set<string>();
  addClaimValues(permissions, user.profile.permission);
  addClaimValues(permissions, user.profile.permissions);
  addSpaceSeparatedClaimValues(permissions, user.profile.scope);
  addSpaceSeparatedClaimValues(permissions, user.profile.scp);
  addAdminRole(permissions, user.profile.role);

  return [...permissions];
}

function addClaimValues(target: Set<string>, value: unknown): void {
  if (typeof value === 'string' && value.trim()) {
    target.add(value.trim());
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((entry) => addClaimValues(target, entry));
  }
}

function addSpaceSeparatedClaimValues(target: Set<string>, value: unknown): void {
  if (typeof value !== 'string') {
    addClaimValues(target, value);
    return;
  }

  value.split(' ').forEach((entry) => addClaimValues(target, entry));
}

function addAdminRole(target: Set<string>, value: unknown): void {
  const roles = new Set<string>();
  addClaimValues(roles, value);

  if ([...roles].some((role) => role.toLowerCase() === 'admin')) {
    target.add('*');
  }
}
