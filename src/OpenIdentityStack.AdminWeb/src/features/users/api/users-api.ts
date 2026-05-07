/**
 * Users API Client
 * 
 * Provides functions for interacting with the user management endpoints
 */

import { apiClient } from '@/lib/api/client';
import type {
  User,
  UserListResponse,
  CreateUserRequest,
  UpdateUserRequest,
  DisableUserRequest,
  ResetPasswordRequest,
  LinkUpstreamIdentityRequest,
  UserStatusChangeResponse,
  PasswordResetResponse,
  UpstreamIdentity,
  LinkUpstreamIdentityResponse,
  PaginationParams,
  Role,
  Group,
} from '@/types';

/**
 * Get paginated list of users
 */
export async function getUsers(
  params?: PaginationParams
): Promise<UserListResponse> {
  return await apiClient.get<UserListResponse>('/api/admin/users', params);
}

/**
 * Get a single user by ID
 */
export async function getUser(userId: string): Promise<User> {
  return await apiClient.get<User>(`/api/admin/users/${userId}`);
}

/**
 * Create a new user
 */
export async function createUser(data: CreateUserRequest): Promise<User> {
  return await apiClient.post<User>('/api/admin/users', data);
}

/**
 * Update an existing user
 */
export async function updateUser(
  userId: string,
  data: UpdateUserRequest
): Promise<User> {
  return await apiClient.put<User>(`/api/admin/users/${userId}`, data);
}

/**
 * Disable a user account
 */
export async function disableUser(
  userId: string,
  data: DisableUserRequest
): Promise<UserStatusChangeResponse> {
  return await apiClient.post<UserStatusChangeResponse>(
    `/api/admin/users/${userId}/disable`,
    data
  );
}

/**
 * Enable a user account
 */
export async function enableUser(
  userId: string
): Promise<UserStatusChangeResponse> {
  return await apiClient.post<UserStatusChangeResponse>(
    `/api/admin/users/${userId}/enable`
  );
}

/**
 * Delete a user account
 */
export async function deleteUser(userId: string): Promise<void> {
  await apiClient.delete(`/api/admin/users/${userId}`);
}

/**
 * Reset a user's password
 */
export async function resetPassword(
  userId: string,
  data: ResetPasswordRequest
): Promise<PasswordResetResponse> {
  return await apiClient.post<PasswordResetResponse>(
    `/api/admin/users/${userId}/reset-password`,
    data
  );
}

/**
 * Get roles assigned to a user
 */
export async function getUserRoles(userId: string): Promise<Role[]> {
  const response = await apiClient.get<{ userId: string; roles: Role[] }>(
    `/api/admin/users/${userId}/roles`
  );
  return response.roles;
}

/**
 * Assign a role to a user
 */
export async function assignRole(
  userId: string,
  roleId: string
): Promise<void> {
  await apiClient.post(`/api/admin/users/${userId}/roles/${roleId}`);
}

/**
 * Unassign a role from a user
 */
export async function unassignRole(
  userId: string,
  roleId: string
): Promise<void> {
  await apiClient.delete(`/api/admin/users/${userId}/roles/${roleId}`);
}

/**
 * Get groups a user belongs to
 */
export async function getUserGroups(userId: string): Promise<Group[]> {
  return await apiClient.get<Group[]>(
    `/api/admin/users/${userId}/groups`
  );
}

/**
 * Get upstream identities linked to a user
 */
export async function getUpstreamIdentities(
  userId: string
): Promise<UpstreamIdentity[]> {
  const response = await apiClient.get<{ items: UpstreamIdentity[] }>(
    `/api/admin/users/${userId}/upstream-identities`
  );
  return response.items;
}

/**
 * Link an upstream identity to a user
 */
export async function linkUpstreamIdentity(
  userId: string,
  data: LinkUpstreamIdentityRequest
): Promise<LinkUpstreamIdentityResponse> {
  return await apiClient.post<LinkUpstreamIdentityResponse>(
    `/api/admin/users/${userId}/upstream-identities`,
    data
  );
}

/**
 * Unlink an upstream identity from a user
 */
export async function unlinkUpstreamIdentity(
  userId: string,
  providerId: string
): Promise<void> {
  await apiClient.delete(
    `/api/admin/users/${userId}/upstream-identities/${providerId}`
  );
}
