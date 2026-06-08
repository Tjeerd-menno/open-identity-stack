import type {
  Group,
  LinkUpstreamIdentityRequest,
  UpstreamIdentity,
  User,
  UserListResponse,
  CreateUserRequest,
  UpdateUserRequest,
  DisableUserRequest,
  ResetPasswordRequest,
  UserStatusChangeResponse,
  PasswordResetResponse,
  LinkUpstreamIdentityResponse,
  Role,
  PaginationParams,
} from '@/types';
import { adminApiClient } from '@/lib/api/client';
import { createUsersContract } from '@openidentitystack/admin-api-client';

const contract = createUsersContract(adminApiClient);

export type {
  User,
  UserListResponse,
  CreateUserRequest,
  UpdateUserRequest,
  DisableUserRequest,
  ResetPasswordRequest,
  UserStatusChangeResponse,
  PasswordResetResponse,
  LinkUpstreamIdentityResponse,
  UpstreamIdentity,
  Group,
  Role,
  PaginationParams,
};

export async function getUsers(params?: PaginationParams): Promise<UserListResponse> {
  const response = await contract.getUsers(params as Parameters<typeof contract.getUsers>[0]);
  return response as UserListResponse;
}

export async function getUser(userId: string): Promise<User> {
  return contract.getUser(userId) as Promise<User>;
}

export async function createUser(data: CreateUserRequest): Promise<User> {
  return contract.createUser(data);
}

export async function updateUser(userId: string, data: UpdateUserRequest): Promise<User> {
  return contract.updateUser(userId, data as never) as Promise<User>;
}

export async function disableUser(userId: string, data: DisableUserRequest): Promise<UserStatusChangeResponse> {
  return contract.disableUser(userId, data) as Promise<UserStatusChangeResponse>;
}

export async function enableUser(userId: string): Promise<UserStatusChangeResponse> {
  return contract.enableUser(userId) as Promise<UserStatusChangeResponse>;
}

export async function deleteUser(userId: string): Promise<void> {
  await contract.deleteUser(userId);
}

export async function resetPassword(userId: string, data: ResetPasswordRequest): Promise<PasswordResetResponse> {
  return contract.resetUserPassword(userId, data) as Promise<PasswordResetResponse>;
}

export async function getUserRoles(userId: string): Promise<Role[]> {
  return contract.getUserRoles(userId) as Promise<Role[]>;
}

export async function assignRole(userId: string, roleId: string): Promise<void> {
  await contract.assignUserRole(userId, roleId);
}

export async function unassignRole(userId: string, roleId: string): Promise<void> {
  await contract.unassignUserRole(userId, roleId);
}

export async function getUserGroups(userId: string): Promise<Group[]> {
  return contract.getUserGroups(userId) as Promise<Group[]>;
}

export async function getUpstreamIdentities(userId: string): Promise<UpstreamIdentity[]> {
  return contract.getUserUpstreamIdentities(userId) as Promise<UpstreamIdentity[]>;
}

export async function linkUpstreamIdentity(
  userId: string,
  data: LinkUpstreamIdentityRequest
): Promise<LinkUpstreamIdentityResponse> {
  return contract.linkUserUpstreamIdentity(userId, data) as Promise<LinkUpstreamIdentityResponse>;
}

export async function unlinkUpstreamIdentity(userId: string, providerId: string): Promise<void> {
  await contract.unlinkUserUpstreamIdentity(userId, providerId);
}
