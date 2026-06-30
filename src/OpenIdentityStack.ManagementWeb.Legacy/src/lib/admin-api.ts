import {
  createAdminApiClient,
  type AdminApiClient,
  formatApiError,
  isApiError,
  type PaginatedResponse,
  type RequestParams,
} from '@openidentitystack/admin-api-client';
import { getApiBaseUrl } from './runtime-config';

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

export async function request<T>(path: string, options: RequestInit = {}, params?: RequestParams): Promise<T> {
  return client.request<T>(path, options, params);
}

export const adminApiClient: AdminApiClient = {
  request,
  get: <T>(path: string, params?: RequestParams) => request<T>(path, {}, params),
  post: <T, TBody = unknown>(path: string, body?: TBody) =>
    request<T>(path, {
      method: 'POST',
      body: body === undefined ? undefined : JSON.stringify(body),
    }),
  put: <T, TBody = unknown>(path: string, body?: TBody) =>
    request<T>(path, { method: 'PUT', body: body === undefined ? undefined : JSON.stringify(body) }),
  patch: <T, TBody = unknown>(path: string, body?: TBody) =>
    request<T>(path, { method: 'PATCH', body: body === undefined ? undefined : JSON.stringify(body) }),
  delete: <T>(path: string, params?: RequestParams) => request<T>(path, { method: 'DELETE' }, params),
};

export function listRoles(): Promise<PaginatedResponse<RoleListItem>> {
  return request<PaginatedResponse<RoleListItem>>('/api/admin/roles', {}, { page: 1, pageSize: 100 });
}
