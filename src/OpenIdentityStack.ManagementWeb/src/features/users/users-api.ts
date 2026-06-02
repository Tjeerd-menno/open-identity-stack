import { request, type PaginatedResponse } from '@/lib/admin-api';

export type UserStatus = 'PendingVerification' | 'Active' | 'Disabled' | 'Locked';

export type UserListItem = {
  id: string;
  email: string;
  displayName: string;
  status: UserStatus;
  createdAt: string;
};

export type UserProfile = Record<string, unknown>;

export type User = UserListItem & {
  mfaEnabled: boolean;
  lastLoginAt: string | null;
  modifiedAt: string | null;
  profile: UserProfile;
};

export type RoleListItem = {
  id: string;
  name: string;
  displayName: string;
  isSystemRole: boolean;
  isActive: boolean;
};

export type UserGroup = {
  id: string;
  name: string;
  displayName?: string | null;
  description?: string | null;
  memberCount?: number;
};

export type UpstreamIdentity = {
  providerId: string;
  providerName?: string | null;
  subject: string;
  displayName?: string | null;
  linkedAt?: string | null;
};

export type UserListParams = {
  page?: number;
  pageSize?: number;
  search?: string;
};

export type CreateUserRequest = {
  email: string;
  displayName: string;
  password: string;
};

export type UpdateUserRequest = {
  displayName: string;
};

export type DisableUserRequest = {
  reason: string;
};

export type ResetPasswordRequest = {
  newPassword: string;
};

export type LinkUpstreamIdentityRequest = {
  providerId: string;
  subject: string;
};

export type UserStatusChangeResponse = {
  userId: string;
  status: UserStatus;
};

export type PasswordResetResponse = {
  userId: string;
  temporaryPassword?: string;
};

export function getUsers(params?: UserListParams): Promise<PaginatedResponse<UserListItem>> {
  return request<PaginatedResponse<UserListItem>>('/api/admin/users', {}, params);
}

export function getUser(userId: string): Promise<User> {
  return request<User>(`/api/admin/users/${userId}`);
}

export function createUser(data: CreateUserRequest): Promise<User> {
  return request<User>('/api/admin/users', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updateUser(userId: string, data: UpdateUserRequest): Promise<User> {
  return request<User>(`/api/admin/users/${userId}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export function disableUser(userId: string, data: DisableUserRequest): Promise<UserStatusChangeResponse> {
  return request<UserStatusChangeResponse>(`/api/admin/users/${userId}/disable`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function enableUser(userId: string): Promise<UserStatusChangeResponse> {
  return request<UserStatusChangeResponse>(`/api/admin/users/${userId}/enable`, {
    method: 'POST',
  });
}

export function deleteUser(userId: string): Promise<void> {
  return request<void>(`/api/admin/users/${userId}`, {
    method: 'DELETE',
  });
}

export function resetUserPassword(userId: string, data: ResetPasswordRequest): Promise<PasswordResetResponse> {
  return request<PasswordResetResponse>(`/api/admin/users/${userId}/reset-password`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export async function getUserRoles(userId: string): Promise<RoleListItem[]> {
  const response = await request<{ userId: string; roles: RoleListItem[] }>(`/api/admin/users/${userId}/roles`);
  return response.roles;
}

export function assignUserRole(userId: string, roleId: string): Promise<void> {
  return request<void>(`/api/admin/users/${userId}/roles/${roleId}`, {
    method: 'POST',
  });
}

export function unassignUserRole(userId: string, roleId: string): Promise<void> {
  return request<void>(`/api/admin/users/${userId}/roles/${roleId}`, {
    method: 'DELETE',
  });
}

export function getUserGroups(userId: string): Promise<UserGroup[]> {
  return request<UserGroup[]>(`/api/admin/users/${userId}/groups`);
}

export async function getUserUpstreamIdentities(userId: string): Promise<UpstreamIdentity[]> {
  const response = await request<{ items: UpstreamIdentity[] }>(`/api/admin/users/${userId}/upstream-identities`);
  return response.items;
}

export function linkUserUpstreamIdentity(
  userId: string,
  data: LinkUpstreamIdentityRequest
): Promise<UpstreamIdentity> {
  return request<UpstreamIdentity>(`/api/admin/users/${userId}/upstream-identities`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function unlinkUserUpstreamIdentity(userId: string, providerId: string): Promise<void> {
  return request<void>(`/api/admin/users/${userId}/upstream-identities/${providerId}`, {
    method: 'DELETE',
  });
}
