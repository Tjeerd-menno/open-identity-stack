/**
 * T051: Create AuthContext with UserManager from oidc-client-ts
 * 
 * React Context for managing OAuth2/OIDC authentication state.
 * Provides authentication methods and user information to the app.
 */

import React, { createContext, useCallback, useEffect, useMemo, useState } from 'react';
import { User, UserManager } from 'oidc-client-ts';
import { oidcConfig, extractPermissions, extractDisplayName } from './services/oidc-config';
import { apiClient } from '@/lib/api/client';

// E2E Test Mode - bypasses real OIDC authentication
const isE2ETestMode = import.meta.env.VITE_E2E_TEST_MODE === 'true';

// Debug logging for E2E test mode detection
console.log('[AuthContext] VITE_E2E_TEST_MODE:', import.meta.env.VITE_E2E_TEST_MODE);
console.log('[AuthContext] isE2ETestMode:', isE2ETestMode);

/**
 * Authenticated user information
 */
export interface AuthenticatedUser {
  sub: string; // Subject (user ID)
  email: string;
  name: string; // Display name
  permissions: string[]; // Granted permissions
}

/**
 * Authentication state
 */
export interface AuthState {
  isAuthenticated: boolean;
  isLoading: boolean;
  isLoggingOut: boolean; // True when logout is in progress
  user: AuthenticatedUser | null;
  accessToken: string | null;
  error: string | null;
}

/**
 * Authentication context value
 */
export interface AuthContextValue extends AuthState {
  login: () => Promise<void>;
  logout: () => Promise<void>;
  signinCallback: () => Promise<void>;
  signinSilentCallback: () => Promise<void>;
  getAccessToken: () => Promise<string | null>;
}

/**
 * Initial authentication state
 */
const initialAuthState: AuthState = {
  isAuthenticated: false,
  isLoading: true,
  isLoggingOut: false,
  user: null,
  accessToken: null,
  error: null,
};

/**
 * Authentication context
 */
export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

/**
 * Props for AuthProvider component
 */
export interface AuthProviderProps {
  children: React.ReactNode;
}

/**
 * Authentication provider component
 * 
 * Manages OIDC authentication state and provides auth methods to the app.
 * 
 * Features:
 * - OAuth2/OIDC login flow with PKCE
 * - Automatic token renewal
 * - Token storage in memory (secure)
 * - User information extraction from claims
 * 
 * @example
 * ```tsx
 * <AuthProvider>
 *   <App />
 * </AuthProvider>
 * ```
 */
export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  console.log('[AuthProvider] Rendering, isE2ETestMode:', isE2ETestMode);

  return isE2ETestMode ? (
    <MockAuthProvider>{children}</MockAuthProvider>
  ) : (
    <OidcAuthProvider>{children}</OidcAuthProvider>
  );
};

const MockAuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  console.log('[AuthProvider] E2E Test Mode active - using mock auth');

  const mockValue = useMemo<AuthContextValue>(() => ({
    isAuthenticated: true,
    isLoading: false,
    isLoggingOut: false,
    user: {
      sub: 'e2e-test-user-id',
      email: 'admin@example.com',
      name: 'E2E Test Admin',
      permissions: [
        'users:read', 'users:create', 'users:update', 'users:delete',
        'roles:read', 'roles:create', 'roles:update', 'roles:delete',
        'groups:read', 'groups:create', 'groups:update', 'groups:delete',
        'service-accounts:read', 'service-accounts:create', 'service-accounts:update', 'service-accounts:delete',
        'providers:read', 'providers:create', 'providers:update', 'providers:delete',
        'sessions:read', 'sessions:revoke',
      ],
    },
    accessToken: 'e2e-test-token',
    error: null,
    login: async () => { console.log('[E2E] Mock login'); },
    logout: async () => { console.log('[E2E] Mock logout'); },
    signinCallback: async () => { console.log('[E2E] Mock signinCallback'); },
    signinSilentCallback: async () => { console.log('[E2E] Mock signinSilentCallback'); },
    getAccessToken: async () => 'e2e-test-token',
  }), []);

  return <AuthContext.Provider value={mockValue}>{children}</AuthContext.Provider>;
};

const OidcAuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [authState, setAuthState] = useState<AuthState>(initialAuthState);
  const [userManager] = useState(() => new UserManager(oidcConfig));
  const forceLoginKey = 'admin_force_login';

  /**
   * Convert OIDC User to AuthenticatedUser
   */
  const mapUser = useCallback((oidcUser: User | null): AuthenticatedUser | null => {
    if (!oidcUser || oidcUser.expired) return null;

    return {
      sub: oidcUser.profile.sub,
      email: oidcUser.profile.email || '',
      name: extractDisplayName(oidcUser),
      permissions: extractPermissions(oidcUser),
    };
  }, []);

  /**
   * Update authentication state from OIDC user
   */
  const updateAuthState = useCallback(
    (oidcUser: User | null) => {
      const user = mapUser(oidcUser);
      setAuthState((prev) => ({
        ...prev,
        isAuthenticated: !!user,
        isLoading: false,
        user,
        accessToken: oidcUser?.access_token || null,
        error: null,
        // Preserve isLoggingOut state - don't reset it here
      }));
    },
    [mapUser]
  );

  /**
   * Get current access token for API client
   */
  const getAccessToken = useCallback(async (): Promise<string | null> => {
    try {
      const user = await userManager.getUser();
      if (!user || user.expired) {
        return null;
      }
      return user.access_token;
    } catch (error) {
      console.error('Error getting access token:', error);
      return null;
    }
  }, [userManager]);

  /**
   * Initiate logout flow
   * Clears local session and redirects to OpenIddict logout endpoint
   */
  const logout = useCallback(async () => {
    try {
      console.log('[Logout] Starting logout flow...');
      
      // Set isLoggingOut to prevent ProtectedRoute from redirecting to /login
      // This must happen BEFORE we remove the user from storage
      setAuthState((prev) => ({
        ...prev,
        isLoggingOut: true,
      }));
      
      sessionStorage.setItem(forceLoginKey, 'true');
      
      // Get the current user to extract the id_token before removing
      const user = await userManager.getUser();
      console.log('[Logout] Current user:', user ? 'found' : 'not found');
      console.log('[Logout] id_token available:', user?.id_token ? 'yes' : 'no');
      
      const idTokenHint = user?.id_token;
      
      // Build the signout redirect URL manually for debugging
      const settings = userManager.settings;
      console.log('[Logout] end_session_endpoint:', settings.metadata?.end_session_endpoint);
      console.log('[Logout] post_logout_redirect_uri:', settings.post_logout_redirect_uri);
      
      // IMPORTANT: Remove the user from local storage BEFORE redirecting
      // This ensures that when we return from the logout endpoint, the user is not re-authenticated
      // from browser storage.
      console.log('[Logout] Clearing user from browser storage...');
      await userManager.removeUser();
      console.log('[Logout] User cleared from storage');
      
      // Build the logout URL manually to ensure we do a full page redirect
      const logoutUrl = new URL(settings.metadata?.end_session_endpoint || `${settings.authority}/connect/logout`);
      if (idTokenHint) {
        logoutUrl.searchParams.set('id_token_hint', idTokenHint);
      }
      if (settings.post_logout_redirect_uri) {
        logoutUrl.searchParams.set('post_logout_redirect_uri', settings.post_logout_redirect_uri);
      }
      
      console.log('[Logout] Redirecting to:', logoutUrl.toString());
      window.location.href = logoutUrl.toString();
      // Don't do anything after this - the browser will navigate away
    } catch (error) {
      console.error('[Logout] Logout error:', error);
      setAuthState((prev) => ({
        ...prev,
        isLoading: false,
        isLoggingOut: false,
        error: error instanceof Error ? error.message : 'Logout failed',
      }));
    }
  }, [userManager]);

  /**
   * T060-T061: Configure API client with token provider and logout handler
   */
  useEffect(() => {
    // Set token provider for API client
    apiClient.setTokenProvider(getAccessToken);
    
    // Set logout handler for 401 responses
    apiClient.setLogoutHandler(() => {
      console.warn('API returned 401, initiating logout...');
      logout();
    });
  }, [getAccessToken, logout]);

  /**
   * Initialize authentication on mount
   */
  useEffect(() => {
    const initAuth = async () => {
      try {
        const user = await userManager.getUser();
        updateAuthState(user);
      } catch (error) {
        console.error('Error initializing auth:', error);
        setAuthState({
          ...initialAuthState,
          isLoading: false,
          error: error instanceof Error ? error.message : 'Authentication initialization failed',
        });
      }
    };

    initAuth();
  }, [userManager, updateAuthState]);

  /**
   * Setup event listeners for user state changes
   */
  useEffect(() => {
    // User loaded event (after login or silent renew)
    const handleUserLoaded = (user: User) => {
      console.log('User loaded:', user.profile.email);
      updateAuthState(user);
    };

    // User unloaded event (after logout)
    const handleUserUnloaded = () => {
      console.log('User unloaded');
      updateAuthState(null);
    };

    // Silent renew error
    const handleSilentRenewError = (error: Error) => {
      console.error('Silent renew error:', error);
      setAuthState((prev) => ({
        ...prev,
        error: 'Token renewal failed. Please login again.',
      }));
    };

    // Access token expiring event
    const handleAccessTokenExpiring = () => {
      console.log('Access token expiring, attempting silent renewal...');
    };

    // Access token expired event
    const handleAccessTokenExpired = () => {
      console.log('Access token expired');
      setAuthState((prev) => ({
        ...prev,
        isAuthenticated: false,
        user: null,
        accessToken: null,
      }));
    };

    // Register event listeners
    userManager.events.addUserLoaded(handleUserLoaded);
    userManager.events.addUserUnloaded(handleUserUnloaded);
    userManager.events.addSilentRenewError(handleSilentRenewError);
    userManager.events.addAccessTokenExpiring(handleAccessTokenExpiring);
    userManager.events.addAccessTokenExpired(handleAccessTokenExpired);

    // Cleanup
    return () => {
      userManager.events.removeUserLoaded(handleUserLoaded);
      userManager.events.removeUserUnloaded(handleUserUnloaded);
      userManager.events.removeSilentRenewError(handleSilentRenewError);
      userManager.events.removeAccessTokenExpiring(handleAccessTokenExpiring);
      userManager.events.removeAccessTokenExpired(handleAccessTokenExpired);
    };
  }, [userManager, updateAuthState]);

  /**
   * Initiate login flow
   * Redirects to OpenIddict authorization endpoint
   */
  const login = useCallback(async () => {
    try {
      setAuthState((prev) => ({ ...prev, isLoading: true, error: null }));
      const forceLogin = sessionStorage.getItem(forceLoginKey) === 'true';
      if (forceLogin) {
        sessionStorage.removeItem(forceLoginKey);
      }

      await userManager.signinRedirect({
        extraQueryParams: forceLogin ? { prompt: 'login', max_age: '0' } : undefined,
      });
    } catch (error) {
      console.error('Login error:', error);
      setAuthState((prev) => ({
        ...prev,
        isLoading: false,
        error: error instanceof Error ? error.message : 'Login failed',
      }));
    }
  }, [userManager]);

  /**
   * Handle callback from authorization endpoint
   * Exchanges authorization code for tokens
   * @throws Error if the callback processing fails
   */
  const signinCallback = useCallback(async () => {
    try {
      const user = await userManager.signinRedirectCallback();
      updateAuthState(user);
    } catch (error) {
      console.error('Signin callback error:', error);
      setAuthState((prev) => ({
        ...prev,
        isLoading: false,
        error: error instanceof Error ? error.message : 'Login callback failed',
      }));
      // Re-throw so the caller (Callback component) can handle it
      throw error;
    }
  }, [userManager, updateAuthState]);

  /**
   * Handle silent callback for token renewal
   */
  const signinSilentCallback = useCallback(async () => {
    try {
      await userManager.signinSilentCallback();
    } catch (error) {
      console.error('Silent callback error:', error);
    }
  }, [userManager]);

  const value = useMemo<AuthContextValue>(() => ({
    ...authState,
    login,
    logout,
    signinCallback,
    signinSilentCallback,
    getAccessToken,
  }), [authState, login, logout, signinCallback, signinSilentCallback, getAccessToken]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};
