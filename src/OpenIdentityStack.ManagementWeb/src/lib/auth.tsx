/* eslint-disable react-refresh/only-export-components */
import { UserManager, WebStorageStateStore, type User, type UserManagerSettings } from 'oidc-client-ts';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useLocation } from 'react-router';
import { setAccessTokenProvider, setUnauthorizedHandler } from './admin-api';
import { extractGrantedPermissions } from './auth-claims';
import { AuthContextProvider, type AuthContextValue } from './auth-context';
export { AuthContextProvider, useAuth, type AuthContextValue } from './auth-context';

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
    setUnauthorizedHandler(value.logout);
  }, [value]);

  return <AuthContextProvider value={value}>{children}</AuthContextProvider>;
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

  useEffect(() => {
    setUnauthorizedHandler(value.logout);
  }, [value.logout]);

  return <AuthContextProvider value={value}>{children}</AuthContextProvider>;
}

function extractPermissions(user: User | null): string[] {
  if (!user) {
    return [];
  }

  return extractGrantedPermissions({
    profile: user.profile,
    accessToken: user.access_token,
  });
}
