import { createApiError, type ApiError } from './api-errors';

type RequestParams = Record<string, string | number | boolean | undefined>;

export type { ApiError } from './api-errors';
export { formatApiError as getApiErrorMessage, isApiError } from './api-errors';

export type PaginatedResponse<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export type RoleListItem = {
  id: string;
  name: string;
  displayName: string;
  isSystemRole: boolean;
  isActive: boolean;
};

let accessTokenProvider: (() => Promise<string | null>) | null = null;
let unauthorizedHandler: (() => void) | null = null;

export function setAccessTokenProvider(provider: () => Promise<string | null>): void {
  accessTokenProvider = provider;
}

export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

function getApiBaseUrl(): string {
  return import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';
}

function buildUrl(path: string, params?: RequestParams): string {
  const url = new URL(path, getApiBaseUrl());

  Object.entries(params ?? {}).forEach(([key, value]) => {
    if (value !== undefined) {
      url.searchParams.set(key, String(value));
    }
  });

  return url.toString();
}

function toApiError(response: Response, payload: unknown): ApiError {
  return createApiError(response.status, payload);
}

export async function request<T>(path: string, options: RequestInit = {}, params?: RequestParams): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set('Content-Type', 'application/json');

  const accessToken = accessTokenProvider ? await accessTokenProvider() : null;
  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  }

  const response = await fetch(buildUrl(path, params), {
    ...options,
    headers,
  });

  if (!response.ok) {
    let payload: unknown = null;
    try {
      payload = await response.json();
    } catch {
      payload = null;
    }

    if (response.status === 401) {
      unauthorizedHandler?.();
    }

    throw toApiError(response, payload);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const body = await response.text();
  if (!body) {
    return undefined as T;
  }

  return JSON.parse(body) as T;
}

export function listRoles(): Promise<PaginatedResponse<RoleListItem>> {
  return request<PaginatedResponse<RoleListItem>>('/api/admin/roles', {}, { page: 1, pageSize: 100 });
}
