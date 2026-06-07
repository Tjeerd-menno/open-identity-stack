import {
  createAdminApiClient,
  formatApiError,
  isApiError,
  type PaginatedResponse,
  type RequestParams,
} from '@openidentitystack/admin-api-client';

export type { ApiError, PaginatedResponse } from '@openidentitystack/admin-api-client';
export { isApiError };
export const getApiErrorMessage = formatApiError;

export type RoleListItem = {
  id: string;
  name: string;
  displayName: string;
  isSystemRole: boolean;
  isActive: boolean;
};

let accessTokenProvider: (() => Promise<string | null>) | null = null;
let unauthorizedHandler: (() => void) | null = null;

const client = createAdminApiClient({
  baseUrl: getApiBaseUrl,
  getAccessToken: () => (accessTokenProvider ? accessTokenProvider() : Promise.resolve(null)),
  onUnauthorized: () => unauthorizedHandler?.(),
});

export function setAccessTokenProvider(provider: () => Promise<string | null>): void {
  accessTokenProvider = provider;
}

export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

function getApiBaseUrl(): string {
  return import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';
}

export async function request<T>(path: string, options: RequestInit = {}, params?: RequestParams): Promise<T> {
  return client.request<T>(path, options, params);
}

export function listRoles(): Promise<PaginatedResponse<RoleListItem>> {
  return request<PaginatedResponse<RoleListItem>>('/api/admin/roles', {}, { page: 1, pageSize: 100 });
}
