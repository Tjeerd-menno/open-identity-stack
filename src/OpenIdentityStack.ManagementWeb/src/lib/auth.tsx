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
  | { token: string; subject: string | null; status: 'success'; user: CurrentUserResponse }
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
  // getUser() reads and parses sessionStorage. API calls only need the session
  // already delivered by the initial load and the user-manager events below.
  const sessionRef = useRef<User | null>(null);

  const adoptSession = useCallback((user: User | null) => {
    const active = user && !user.expired ? user : null;
    sessionRef.current = active;
    setOidcUser(active);
  }, []);

  const clearSession = useCallback(() => {
    sessionRef.current = null;
    setOidcUser(null);
    setCurrentUserLoad(null);
  }, []);

  const getAccessToken = useCallback(() => {
    const user = sessionRef.current;
    return Promise.resolve(user && !user.expired ? user.access_token : null);
  }, []);

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
            adoptSession(user);
            setIsLoading(false);
          }
        } catch (error) {
          console.error('Failed to get user:', error);
          if (!cancelled) {
            adoptSession(null);
            setIsLoading(false);
          }
        }
      }
    }

    processAuthCallback();

    return () => {
      cancelled = true;
    };
  }, [adoptSession, userManager, location.pathname]);

  useEffect(() => {
    const removeUserLoaded = userManager.events.addUserLoaded((user) => {
      adoptSession(user);
    });
    const removeUserUnloaded = userManager.events.addUserUnloaded(() => {
      clearSession();
    });
    const removeAccessTokenExpired = userManager.events.addAccessTokenExpired(() => {
      clearSession();
    });

    return () => {
      removeUserLoaded();
      removeUserUnloaded();
      removeAccessTokenExpired();
    };
  }, [adoptSession, clearSession, userManager]);

  useEffect(() => {
    setAccessTokenProvider(getAccessToken);
  }, [getAccessToken]);

  const accessToken = oidcUser && !oidcUser.expired ? oidcUser.access_token : null;
  const activeSubject = sessionSubject(oidcUser);

  useEffect(() => {
    if (!accessToken) {
      return;
    }

    let cancelled = false;
    const subject = activeSubject;

    api.currentUser
      .getCurrentUser()
      .then((response) => {
        if (!cancelled) {
          setCurrentUserLoad({ token: accessToken, subject, status: 'success', user: response });
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
  }, [accessToken, activeSubject, recoverRejectedSession]);

  // Silent renew mints a new access token for the same subject. Keep the last
  // successful profile until /me returns so AppRoutes does not swap the shell
  // for the full-screen loader. A different subject is a different operator.
  const sameOperator =
    activeSubject !== null &&
    currentUserLoad?.status === 'success' &&
    currentUserLoad.subject === activeSubject;
  const effectiveCurrentUser =
    oidcUser &&
    currentUserLoad?.status === 'success' &&
    (currentUserLoad.token === accessToken || sameOperator)
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

function sessionSubject(user: User | null): string | null {
  const subject = user?.profile.sub;
  return typeof subject === 'string' && subject.length > 0 ? subject : null;
}

function getApiStatus(error: unknown): number | null {
  return typeof error === 'object'
    && error !== null
    && 'status' in error
    && typeof (error as { status?: unknown }).status === 'number'
    ? (error as { status: number }).status
    : null;
}
