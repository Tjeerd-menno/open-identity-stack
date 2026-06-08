import { adminApiClient } from '@/lib/admin-api';
import {
  createUsersContract,
  type Group as SharedUserGroup,
  type CreateUserRequest,
  type DisableUserRequest,
  type PasswordResetResponse,
  type LinkUpstreamIdentityRequest as SharedLinkUpstreamIdentityRequest,
  type PaginatedResponse,
  type RoleListItem,
  type UpstreamIdentity as SharedUpstreamIdentity,
  type UpdateUserRequest,
  type User,
  type UserListItem,
  type UserListParams,
  type UserProfile,
  type UserStatus,
  type UserStatusChangeResponse as SharedUserStatusChangeResponse,
} from '@openidentitystack/admin-api-client';

const contract = createUsersContract(adminApiClient);

export type {
  User,
  UserListItem,
  UserListParams,
  UserProfile,
  UserStatus,
  CreateUserRequest,
  UpdateUserRequest,
  DisableUserRequest,
  PasswordResetResponse,
  RoleListItem,
  PaginatedResponse,
};

export type UserGroup = SharedUserGroup;

export type UpstreamIdentity = SharedUpstreamIdentity & {
  providerName?: string | null;
  subjectId?: string | null;
};

export type UserStatusChangeResponse = SharedUserStatusChangeResponse & {
  status: UserStatus;
};

export type LinkUpstreamIdentityRequest = Omit<SharedLinkUpstreamIdentityRequest, 'subjectId' | 'email'> & {
  subject: string;
};

export function getUsers(params?: UserListParams): Promise<PaginatedResponse<UserListItem>> {
  return contract.getUsers(params);
}

export function getUser(userId: string): Promise<User> {
  return contract.getUser(userId);
}

export function createUser(data: CreateUserRequest): Promise<User> {
  return contract.createUser(data);
}

export function updateUser(userId: string, data: UpdateUserRequest): Promise<User> {
  return contract.updateUser(userId, data);
}

export function disableUser(userId: string, data: DisableUserRequest): Promise<UserStatusChangeResponse> {
  return contract.disableUser(userId, data) as Promise<UserStatusChangeResponse>;
}

export function enableUser(userId: string): Promise<UserStatusChangeResponse> {
  return contract.enableUser(userId) as Promise<UserStatusChangeResponse>;
}

export function deleteUser(userId: string): Promise<void> {
  return contract.deleteUser(userId);
}

export function resetUserPassword(userId: string, data: { newPassword: string }): Promise<PasswordResetResponse> {
  return contract.resetUserPassword(userId, data);
}

export function getUserRoles(userId: string): Promise<RoleListItem[]> {
  return contract.getUserRoles(userId);
}

export function assignUserRole(userId: string, roleId: string): Promise<void> {
  return contract.assignUserRole(userId, roleId);
}

export function unassignUserRole(userId: string, roleId: string): Promise<void> {
  return contract.unassignUserRole(userId, roleId);
}

export function getUserGroups(userId: string): Promise<UserGroup[]> {
  return contract.getUserGroups(userId);
}

export function getUserUpstreamIdentities(userId: string): Promise<UpstreamIdentity[]> {
  return contract.getUserUpstreamIdentities(userId) as Promise<UpstreamIdentity[]>;
}

export function linkUserUpstreamIdentity(
  userId: string,
  data: LinkUpstreamIdentityRequest
): Promise<UpstreamIdentity> {
  return contract.linkUserUpstreamIdentity(userId, {
    providerId: data.providerId,
    subject: data.subject,
  }) as Promise<UpstreamIdentity>;
}

export function unlinkUserUpstreamIdentity(userId: string, providerId: string): Promise<void> {
  return contract.unlinkUserUpstreamIdentity(userId, providerId);
}
