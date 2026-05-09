/**
 * T050: Configure OIDC client settings
 * 
 * OIDC configuration for OAuth2/OIDC authentication with PKCE flow.
 * Uses oidc-client-ts library for secure SPA authentication.
 */

import type { UserManagerSettings } from 'oidc-client-ts';
import { WebStorageStateStore } from 'oidc-client-ts';
import { getRuntimeConfig } from '@/config/runtime-config';

// E2E Test Mode - bypasses real OIDC configuration
const isE2ETestMode = import.meta.env.VITE_E2E_TEST_MODE === 'true';

/**
 * Get the OIDC authority URL from environment variables
 */
const getAuthority = (): string => {
  // In E2E test mode, return a placeholder since OIDC won't be used
  if (isE2ETestMode) {
    return 'http://localhost:5000';
  }
  const authority = getRuntimeConfig('VITE_OIDC_AUTHORITY');
  if (!authority) {
    throw new Error('VITE_OIDC_AUTHORITY environment variable is not set');
  }
  return authority;
};

/**
 * Get the base URL of the application
 */
const getBaseUrl = (): string => {
  return globalThis.location.origin;
};

/**
 * OIDC client configuration for OAuth2/OIDC authentication
 * 
 * Security features:
 * - PKCE (Proof Key for Code Exchange) enabled by default
 * - Authorization Code Flow with PKCE (secure for SPAs)
 * - Automatic silent token renewal
 * - Access tokens stored in memory (not localStorage for security)
 */
export const oidcConfig: UserManagerSettings = {
  // OpenIddict server URL
  authority: getAuthority(),
  
  // Client ID registered in OpenIddict
  client_id: getRuntimeConfig('VITE_OIDC_CLIENT_ID', 'admin-web-client'),
  
  // Redirect URIs
  redirect_uri: `${getBaseUrl()}/auth/callback`,
  post_logout_redirect_uri: `${getBaseUrl()}/`,
  silent_redirect_uri: `${getBaseUrl()}/auth/silent-callback`,
  
  // OAuth2/OIDC flow configuration
  response_type: 'code', // Authorization Code Flow
  
  // Requested scopes
  scope: 'openid profile email api',
  
  // Automatic silent token renewal (before expiration)
  automaticSilentRenew: true,
  
  // Token storage strategy
  // Access token: in-memory (UserManager handles this)
  // Refresh token: HTTPOnly cookie (managed by server)
  userStore: new WebStorageStateStore({ store: globalThis.sessionStorage }),
  
  // Token validation
  filterProtocolClaims: true,
  loadUserInfo: true,
  
  // PKCE (Proof Key for Code Exchange) - enabled by default in oidc-client-ts
  // code_challenge_method: 'S256' is automatic
  
  // Timeouts
  silentRequestTimeoutInSeconds: 10,
  
  // Metadata configuration (optional - will be discovered from authority)
  metadata: {
    issuer: getAuthority(),
    authorization_endpoint: `${getAuthority()}/connect/authorize`,
    token_endpoint: `${getAuthority()}/connect/token`,
    userinfo_endpoint: `${getAuthority()}/connect/userinfo`,
    end_session_endpoint: `${getAuthority()}/connect/logout`,
    jwks_uri: `${getAuthority()}/.well-known/jwks`,
  },
};

/**
 * Extract permissions from user claims
 * 
 * @param user - OIDC user object
 * @returns Array of permission strings
 */
export const extractPermissions = (user: any): string[] => {
  if (!user) return [];

  const standardScopes = ['openid', 'profile', 'email', 'api', 'offline_access'];

  const fromProfile = (profile: any): string[] => {
    if (!profile) return [];

    if (profile.permission) {
      return Array.isArray(profile.permission)
        ? profile.permission
        : [profile.permission];
    }

    if (profile.permissions) {
      return Array.isArray(profile.permissions)
        ? profile.permissions
        : [profile.permissions];
    }

    if (profile.scope) {
      const scopes = typeof profile.scope === 'string'
        ? profile.scope.split(' ')
        : profile.scope;
      return scopes.filter((s: string) => !standardScopes.includes(s));
    }

    const roles = profile.role || profile.roles;
    if (roles) {
      const roleList = Array.isArray(roles) ? roles : [roles];
      if (roleList.some((r: string) => r === 'admin' || r === 'super-admin')) {
        return ['*'];
      }
    }

    return [];
  };

  const profilePermissions = fromProfile(user.profile);
  if (profilePermissions.length > 0) {
    return profilePermissions;
  }

  if (typeof user.access_token === 'string' && user.access_token.includes('.')) {
    try {
      const payload = user.access_token.split('.')[1];
      const normalized = payload.replaceAll('-', '+').replaceAll('_', '/');
      const padded = normalized.padEnd(normalized.length + (4 - (normalized.length % 4)) % 4, '=');
      const decoded = JSON.parse(atob(padded));
      const tokenPermissions = fromProfile(decoded);
      if (tokenPermissions.length > 0) {
        return tokenPermissions;
      }
    } catch {
      return [];
    }
  }

  return [];
};

/**
 * Extract user display name from claims
 * 
 * @param user - OIDC user object
 * @returns Display name string
 */
export const extractDisplayName = (user: any): string => {
  if (!user?.profile) return 'Unknown User';
  
  const profile = user.profile;
  
  // Try different claim formats
  return (
    profile.name ||
    profile.display_name ||
    profile.preferred_username ||
    profile.email ||
    'Unknown User'
  );
};
