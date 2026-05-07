import { getRuntimeConfig } from '@/config/runtime-config';

/**
 * API endpoints
 */
export const API_ENDPOINTS = {
  // Users
  USERS: '/api/admin/users',
  USER: (id: string) => `/api/admin/users/${id}`,
  USER_DISABLE: (id: string) => `/api/admin/users/${id}/disable`,
  USER_ENABLE: (id: string) => `/api/admin/users/${id}/enable`,
  USER_RESET_PASSWORD: (id: string) => `/api/admin/users/${id}/reset-password`,
  USER_ROLES: (id: string) => `/api/admin/users/${id}/roles`,
  USER_ROLE: (userId: string, roleId: string) => `/api/admin/users/${userId}/roles/${roleId}`,
  USER_GROUPS: (id: string) => `/api/admin/users/${id}/groups`,
  USER_UPSTREAM_IDENTITIES: (id: string) => `/api/admin/users/${id}/upstream-identities`,
  USER_LINK_IDENTITY: (id: string) => `/api/admin/users/${id}/link-identity`,
  USER_UNLINK_IDENTITY: (userId: string, providerId: string) =>
    `/api/admin/users/${userId}/upstream-identities/${providerId}`,

  // Roles
  ROLES: '/api/admin/roles',
  ROLE: (id: string) => `/api/admin/roles/${id}`,

  // Groups
  GROUPS: '/api/admin/groups',
  GROUP: (id: string) => `/api/admin/groups/${id}`,
  GROUP_MEMBERS: (id: string) => `/api/admin/groups/${id}/members`,
  GROUP_MEMBER: (groupId: string, userId: string) => `/api/admin/groups/${groupId}/members/${userId}`,
  GROUP_MAPPINGS: (id: string) => `/api/admin/groups/${id}/mappings`,
  GROUP_MAPPING: (groupId: string, mappingId: string) =>
    `/api/admin/groups/${groupId}/mappings/${mappingId}`,

  // Service Accounts
  SERVICE_ACCOUNTS: '/api/admin/service-accounts',
  SERVICE_ACCOUNT: (id: string) => `/api/admin/service-accounts/${id}`,
  SERVICE_ACCOUNT_ENABLE: (id: string) => `/api/admin/service-accounts/${id}/enable`,
  SERVICE_ACCOUNT_DISABLE: (id: string) => `/api/admin/service-accounts/${id}/disable`,
  SERVICE_ACCOUNT_ROTATE_SECRET: (id: string) => `/api/admin/service-accounts/${id}/rotate-secret`,
  SERVICE_ACCOUNT_ADD_CERTIFICATE: (id: string) => `/api/admin/service-accounts/${id}/certificates`,

  // Sessions
  SESSIONS: '/api/admin/sessions',
  SESSION: (id: string) => `/api/admin/sessions/${id}`,
  SESSION_REVOKE: (id: string) => `/api/admin/sessions/${id}/revoke`,
  SESSION_REVOKE_ALL_USER: (userId: string) => `/api/admin/sessions/users/${userId}/revoke-all`,

  // Providers
  PROVIDERS: '/api/admin/providers',
  PROVIDER: (id: string) => `/api/admin/providers/${id}`,
} as const;

/**
 * Permission constants
 */
export const PERMISSIONS = {
  // Users
  USERS_READ: 'users:read',
  USERS_CREATE: 'users:create',
  USERS_UPDATE: 'users:update',
  USERS_DELETE: 'users:delete',
  USERS_MANAGE_ROLES: 'users:manage-roles',
  USERS_MANAGE_GROUPS: 'users:manage-groups',
  USERS_RESET_PASSWORD: 'users:reset-password',

  // Roles
  ROLES_READ: 'roles:read',
  ROLES_CREATE: 'roles:create',
  ROLES_UPDATE: 'roles:update',
  ROLES_DELETE: 'roles:delete',

  // Groups
  GROUPS_READ: 'groups:read',
  GROUPS_CREATE: 'groups:create',
  GROUPS_UPDATE: 'groups:update',
  GROUPS_DELETE: 'groups:delete',
  GROUPS_MANAGE_MEMBERS: 'groups:manage-members',
  GROUPS_MANAGE_MAPPINGS: 'groups:manage-mappings',

  // Service Accounts
  SERVICE_ACCOUNTS_READ: 'service-accounts:read',
  SERVICE_ACCOUNTS_CREATE: 'service-accounts:create',
  SERVICE_ACCOUNTS_UPDATE: 'service-accounts:update',
  SERVICE_ACCOUNTS_DELETE: 'service-accounts:delete',
  SERVICE_ACCOUNTS_ROTATE_SECRET: 'service-accounts:rotate-secret',

  // Sessions
  SESSIONS_READ: 'sessions:read',
  SESSIONS_REVOKE: 'sessions:revoke',

  // Providers
  PROVIDERS_READ: 'providers:read',
  PROVIDERS_CREATE: 'providers:create',
  PROVIDERS_UPDATE: 'providers:update',
  PROVIDERS_DELETE: 'providers:delete',
} as const;

/**
 * App configuration
 */
export const APP_CONFIG = {
  NAME: getRuntimeConfig('VITE_APP_NAME', 'OpenIdentityStack Admin'),
  VERSION: getRuntimeConfig('VITE_APP_VERSION', '1.0.0'),
  DEFAULT_PAGE_SIZE: 20,
  MAX_PAGE_SIZE: 100,
} as const;

/**
 * OIDC configuration
 */
export const OIDC_CONFIG = {
  AUTHORITY: getRuntimeConfig('VITE_OIDC_AUTHORITY', 'https://localhost:5001'),
  CLIENT_ID: getRuntimeConfig('VITE_OIDC_CLIENT_ID', 'admin-web-client'),
  REDIRECT_URI: getRuntimeConfig('VITE_OIDC_REDIRECT_URI', 'http://localhost:5173/auth/callback'),
  POST_LOGOUT_REDIRECT_URI:
    getRuntimeConfig('VITE_OIDC_POST_LOGOUT_REDIRECT_URI', 'http://localhost:5173/'),
  SILENT_REDIRECT_URI:
    getRuntimeConfig('VITE_OIDC_SILENT_REDIRECT_URI', 'http://localhost:5173/auth/silent-callback'),
  SCOPE: getRuntimeConfig('VITE_OIDC_SCOPE', 'openid profile email admin-api'),
} as const;

/**
 * Route paths
 */
export const ROUTES = {
  HOME: '/',
  LOGIN: '/login',
  AUTH_CALLBACK: '/auth/callback',
  AUTH_SILENT_CALLBACK: '/auth/silent-callback',
  DASHBOARD: '/dashboard',

  // Users
  USERS: '/users',
  USER_DETAIL: (id: string) => `/users/${id}`,
  USER_CREATE: '/users/create',
  USER_EDIT: (id: string) => `/users/${id}/edit`,

  // Roles
  ROLES: '/roles',
  ROLE_DETAIL: (id: string) => `/roles/${id}`,
  ROLE_CREATE: '/roles/new',
  ROLE_EDIT: (id: string) => `/roles/${id}/edit`,

  // Groups
  GROUPS: '/groups',
  GROUP_DETAIL: (id: string) => `/groups/${id}`,
  GROUP_CREATE: '/groups/new',
  GROUP_EDIT: (id: string) => `/groups/${id}/edit`,

  // Service Accounts
  SERVICE_ACCOUNTS: '/service-accounts',
  SERVICE_ACCOUNT_DETAIL: (id: string) => `/service-accounts/${id}`,
  SERVICE_ACCOUNT_CREATE: '/service-accounts/new',
  SERVICE_ACCOUNT_EDIT: (id: string) => `/service-accounts/${id}/edit`,

  // Sessions
  SESSIONS: '/sessions',
  SESSION_DETAIL: (id: string) => `/sessions/${id}`,

  // Providers
  PROVIDERS: '/providers',
  PROVIDER_DETAIL: (id: string) => `/providers/${id}`,
  PROVIDER_CREATE: '/providers/new',
  PROVIDER_EDIT: (id: string) => `/providers/${id}/edit`,
} as const;

/**
 * Status colors for badges
 */
export const STATUS_COLORS = {
  Active: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300',
  Disabled: 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-300',
  Locked: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300',
  PendingVerification: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-300',
  Expired: 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-300',
  Revoked: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300',
} as const;
