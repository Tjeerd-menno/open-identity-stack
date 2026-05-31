type RequestParams = Record<string, string | number | boolean | undefined>;

export type PaginatedResponse<T> = {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export type UserStatus = 'PendingVerification' | 'Active' | 'Disabled' | 'Locked';

export type UserListItem = {
  id: string;
  email: string;
  displayName: string;
  status: UserStatus;
  createdAt: string;
};

export type User = UserListItem & {
  mfaEnabled: boolean;
  lastLoginAt: string | null;
  modifiedAt: string | null;
  profile: Record<string, unknown>;
};

export type RoleListItem = {
  id: string;
  name: string;
  displayName: string;
  isSystemRole: boolean;
  isActive: boolean;
};

export type CreateUserRequest = {
  email: string;
  displayName: string;
  password: string;
};

let accessTokenProvider: (() => Promise<string | null>) | null = null;

export function setAccessTokenProvider(provider: () => Promise<string | null>): void {
  accessTokenProvider = provider;
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

async function request<T>(path: string, options: RequestInit = {}, params?: RequestParams): Promise<T> {
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
    let message = `Admin API request failed with status ${response.status}`;
    try {
      const payload = (await response.json()) as { detail?: string; title?: string; message?: string };
      message = payload.detail ?? payload.message ?? payload.title ?? message;
    } catch {
      // Keep the status-based message when the response body is empty or not JSON.
    }

    throw new Error(message);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function listUsers(page = 1, pageSize = 20, search?: string): Promise<PaginatedResponse<UserListItem>> {
  return request<PaginatedResponse<UserListItem>>('/api/admin/users', {}, { page, pageSize, search: search || undefined });
}

export function getUser(userId: string): Promise<User> {
  return request<User>(`/api/admin/users/${userId}`);
}

export function createUser(data: CreateUserRequest): Promise<UserListItem> {
  return request<UserListItem>('/api/admin/users', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updateUser(userId: string, displayName: string): Promise<unknown> {
  return request(`/api/admin/users/${userId}`, {
    method: 'PUT',
    body: JSON.stringify({ displayName }),
  });
}

export function disableUser(userId: string, reason: string): Promise<unknown> {
  return request(`/api/admin/users/${userId}/disable`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
  });
}

export function listUserRoles(userId: string): Promise<{ userId: string; roles: RoleListItem[] }> {
  return request<{ userId: string; roles: RoleListItem[] }>(`/api/admin/users/${userId}/roles`);
}

export function listRoles(): Promise<PaginatedResponse<RoleListItem>> {
  return request<PaginatedResponse<RoleListItem>>('/api/admin/roles', {}, { page: 1, pageSize: 100 });
}

export function assignRole(userId: string, roleId: string): Promise<unknown> {
  return request(`/api/admin/users/${userId}/roles/${roleId}`, {
    method: 'POST',
  });
}
