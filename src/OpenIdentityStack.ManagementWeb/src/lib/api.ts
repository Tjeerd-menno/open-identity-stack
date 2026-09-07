import {
  createAdminApiClient,
  createApplicationPermissionsContract,
  createApplicationsContract,
  createAuditEntriesContract,
  createCurrentUserContract,
  createGroupsContract,
  createProvidersContract,
  createRolesContract,
  createSessionsContract,
  createSettingsContract,
  createUsersContract,
  formatApiError,
  isApiError,
  type ApiError,
} from '@openidentitystack/admin-api-client';
import { getApiBaseUrl } from './runtime-config';

export type { ApiError, PaginatedResponse } from '@openidentitystack/admin-api-client';
export { isApiError };
export const getApiErrorMessage = formatApiError;

let accessTokenProvider: (() => Promise<string | null>) | null = null;
let unauthorizedHandler: (() => void) | null = null;
let administrativeApprovalHandler: ((error: ApiError) => Promise<boolean>) | null = null;

export function setAdministrativeApprovalHandler(handler: ((error: ApiError) => Promise<boolean>) | null): void {
  administrativeApprovalHandler = handler;
}

export function setAccessTokenProvider(provider: () => Promise<string | null>): void {
  accessTokenProvider = provider;
}

export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

const client = createAdminApiClient({
  baseUrl: getApiBaseUrl,
  getAccessToken: () => (accessTokenProvider ? accessTokenProvider() : Promise.resolve(null)),
  onUnauthorized: () => unauthorizedHandler?.(),
  onAdministrativeApprovalRequired: (error) => administrativeApprovalHandler?.(error) ?? Promise.resolve(false),
});

/** Typed access to every Admin API domain, backed by the shared client. */
export const api = {
  users: createUsersContract(client),
  groups: createGroupsContract(client),
  applications: createApplicationsContract(client),
  roles: createRolesContract(client),
  sessions: createSessionsContract(client),
  providers: createProvidersContract(client),
  settings: createSettingsContract(client),
  audit: createAuditEntriesContract(client),
  applicationPermissions: createApplicationPermissionsContract(client),
  currentUser: createCurrentUserContract(client),
};
