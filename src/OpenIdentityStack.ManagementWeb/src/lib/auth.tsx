/* eslint-disable react-refresh/only-export-components */
import { UserManager, WebStorageStateStore, type User, type UserManagerSettings } from 'oidc-client-ts';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useLocation } from 'react-router';
import type { CurrentUserResponse } from '@openidentitystack/admin-api-client';
import { api, setAccessTokenProvider, setUnauthorizedHandler } from './api';
import { AuthContextProvider, type AuthContextValue } from './auth-context';
import { getOidcAuthority, getOidcClientId } from './runtime-config';
import { AdministrativeApprovalDialog } from '@/components/AdministrativeApprovalDialog';

type CurrentUserLoadState =
  | { token: string; status: 'success'; user: CurrentUserResponse }
  | { token: string; status: 'error'; error: unknown }
  | null;

export { AuthContextProvider, useAuth, type AuthContextValue } from './auth-context';

function createUserManager(): UserManager {
  const baseUrl = globalThis.location.origin;
  const settings: UserManagerSettings = {
    authority: getOidcAuthority(),
    client_id: getOidcClientId(),
    redirect_uri: `${baseUrl}/auth/callback`,
    post_logout_redirect_uri: `${baseUrl}/`,
    silent_redirect_uri: `${baseUrl}/auth/silent-callback`,
    response_type: 'code',
    scope: 'openid profile email ois.admin',
    automaticSilentRenew: true,
    userStore: new WebStorageStateStore({ store: globalThis.sessionStorage }),
  };

  return new UserManager(settings);
}

export function AuthProvider({ children }: { children: ReactNode }) {
  return <OidcAuthProvider>{children}</OidcAuthProvider>;
}

// Guards the authorization-code redemption so it runs exactly once, even when
// React StrictMode double-invokes effects in development (a second redemption of
// the same code fails with "authorization code has already been redeemed").
let authCodeRedemptionStarted = false;

function OidcAuthProvider({ children }: { children: ReactNode }) {
  const [userManager] = useState(() => createUserManager());
  const [oidcUser, setOidcUser] = useState<User | null>(null);
  const [currentUserLoad, setCurrentUserLoad] = useState<CurrentUserLoadState>(null);
  const [isLoading, setIsLoading] = useState(true);
  const location = useLocation();
  const recoveryStarted = useRef(false);

  const getAccessToken = useCallback(async () => {
    const user = await userManager.getUser();
    return user?.expired ? null : (user?.access_token ?? null);
  }, [userManager]);

  const login = useCallback(async () => userManager.signinRedirect(), [userManager]);
  const logout = useCallback(async () => userManager.signoutRedirect(), [userManager]);
  const reauthenticate = useCallback(async () => {
    sessionStorage.setItem('administrative-approval-return', location.pathname + location.search);
    await userManager.signinRedirect({ prompt: 'login', max_age: 0 });
  }, [location.pathname, location.search, userManager]);
  const recoverRejectedSession = useCallback(async () => {
    if (recoveryStarted.current) return;
    recoveryStarted.current = true;
    try {
      await userManager.removeUser();
      setOidcUser(null);
      await reauthenticate();
    } catch (error) {
      recoveryStarted.current = false;
      console.error('Unable to restart authentication:', error);
    }
  }, [reauthenticate, userManager]);

  useEffect(() => {
    let cancelled = false;

    async function processAuthCallback() {
      if (location.pathname === '/auth/callback') {
        if (authCodeRedemptionStarted) {
          return;
        }
        authCodeRedemptionStarted = true;
        try {
          await userManager.signinCallback();
        } catch (error) {
          console.error('OIDC callback error:', error);
        } finally {
          // Redeemed (or failed) — reload at the app root; init below reads the
          // stored user from sessionStorage and resolves the authenticated state.
          const returnPath = sessionStorage.getItem('administrative-approval-return');
          sessionStorage.removeItem('administrative-approval-return');
          window.location.replace(returnPath?.startsWith('/') && !returnPath.startsWith('//') && !returnPath.includes('\\') ? returnPath : '/');
        }
      } else {
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
    const removeUserLoaded = userManager.events.addUserLoaded((user) => {
      setOidcUser(user && !user.expired ? user : null);
    });

    return () => {
      removeUserLoaded();
    };
  }, [userManager]);

  useEffect(() => {
    setAccessTokenProvider(getAccessToken);
  }, [getAccessToken]);

  const accessToken = oidcUser && !oidcUser.expired ? oidcUser.access_token : null;

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    let cancelled = false;

    api.currentUser
      .getCurrentUser()
      .then((response) => {
        if (!cancelled) {
          setCurrentUserLoad({ token: accessToken, status: 'success', user: response });
        }
      })
      .catch((error: unknown) => {
        if (cancelled) {
          return;
        }

        if (getApiStatus(error) === 401) {
          void recoverRejectedSession();
        } else {
          console.error('Failed to load current user:', error);
          setCurrentUserLoad({ token: accessToken, status: 'error', error });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [accessToken, recoverRejectedSession]);

  const effectiveCurrentUser =
    currentUserLoad?.token === accessToken && currentUserLoad.status === 'success'
      ? currentUserLoad.user
      : null;
  const effectiveCurrentUserError =
    currentUserLoad?.token === accessToken && currentUserLoad.status === 'error'
      ? currentUserLoad.error
      : null;
  const isAuthLoading =
    isLoading || (!!oidcUser && !effectiveCurrentUser && !effectiveCurrentUserError);

  const value = useMemo<AuthContextValue>(
    () => ({
      isAuthenticated: !!oidcUser,
      isLoading: isAuthLoading,
      displayName: effectiveCurrentUser?.displayName ?? getFallbackDisplayName(oidcUser),
      permissions: effectiveCurrentUser?.permissions ?? [],
      login,
      logout,
      getAccessToken,
    }),
    [effectiveCurrentUser, getAccessToken, isAuthLoading, login, logout, oidcUser]
  );

  useEffect(() => {
    setUnauthorizedHandler(recoverRejectedSession);
  }, [recoverRejectedSession]);

  if (effectiveCurrentUserError) {
    return (
      <div role="alert">
        Unable to load your authorization state.
      </div>
    );
  }

  return <AuthContextProvider value={value}>{children}<AdministrativeApprovalDialog onReauthenticate={reauthenticate} /></AuthContextProvider>;
}

function getFallbackDisplayName(user: User | null): string {
  return typeof user?.profile.name === 'string' ? user.profile.name : 'Operator';
}

function getApiStatus(error: unknown): number | null {
  return typeof error === 'object'
    && error !== null
    && 'status' in error
    && typeof (error as { status?: unknown }).status === 'number'
    ? (error as { status: number }).status
    : null;
}
