import type { AdminApiClient, PaginatedResponse } from './index';

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

export type IdentityMigrationLink = {
  providerId: string;
  providerName: string;
  subjectId: string;
  issuer: string | null;
  associationEvidence: string;
  isQuarantined: boolean;
};

export type IdentityMigrationUser = {
  userId: string;
  displayName: string;
  status: string;
  hasPasswordCredential: boolean;
  candidateFederationProviderIds: string[];
  migrationBlocked: boolean;
  recoveryRequired: boolean;
  identities: IdentityMigrationLink[];
};

export type IdentityMigrationInventory = {
  items: IdentityMigrationUser[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type UpstreamIdentity = {
  issuer?: string | null;
  associationEvidence?: string;
  isQuarantined?: boolean;
  providerId: string;
  providerName?: string | null;
  subject: string;
  subjectId?: string;
  email?: string | null;
  linkedAt?: string | null;
  lastLoginAt?: string | null;
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
  profile?: Record<string, unknown>;
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

export type UserStatusChangeResponse = {
  userId: string;
  status?: UserStatus;
  timestamp?: string;
};

export type PasswordResetResponse = {
  userId: string;
  temporaryPassword?: string;
};

export type UsersContract = {
  getUsers: (params?: UserListParams) => Promise<PaginatedResponse<UserListItem>>;
  getUser: (userId: string) => Promise<User>;
  createUser: (data: CreateUserRequest) => Promise<User>;
  updateUser: (userId: string, data: UpdateUserRequest) => Promise<User>;
  disableUser: (userId: string, data: DisableUserRequest) => Promise<UserStatusChangeResponse>;
  enableUser: (userId: string) => Promise<UserStatusChangeResponse>;
  deleteUser: (userId: string) => Promise<void>;
  resetUserPassword: (userId: string, data: ResetPasswordRequest) => Promise<PasswordResetResponse>;
  getUserRoles: (userId: string) => Promise<RoleListItem[]>;
  assignUserRole: (userId: string, roleId: string) => Promise<void>;
  unassignUserRole: (userId: string, roleId: string) => Promise<void>;
  getUserGroups: (userId: string) => Promise<UserGroup[]>;
  getIdentityMigrationInventory: (params: { providerId?: string; page?: number; pageSize?: number }) => Promise<IdentityMigrationInventory>;
  getUserUpstreamIdentities: (userId: string) => Promise<UpstreamIdentity[]>;
  unlinkUserUpstreamIdentity: (userId: string, providerId: string) => Promise<void>;
};

function normalizeUpstreamIdentityListResponse(response: { items?: UpstreamIdentity[] } | UpstreamIdentity[]): UpstreamIdentity[] {
  return Array.isArray(response) ? response : (response.items ?? []);
}

function normalizeUserRolesResponse(response: { userId?: string; roles?: RoleListItem[] } | RoleListItem[]): RoleListItem[] {
  return Array.isArray(response) ? response : (response.roles ?? []);
}

export function createUsersContract(client: AdminApiClient): UsersContract {
  return {
    getUsers: (params?: UserListParams) => client.get<PaginatedResponse<UserListItem>>('/api/admin/users', params),
    getUser: (userId) => client.get<User>(`/api/admin/users/${userId}`),
    createUser: (data) => client.post<User>('/api/admin/users', data),
    updateUser: (userId, data) => client.put<User>(`/api/admin/users/${userId}`, data),
    disableUser: (userId, data) => client.post<UserStatusChangeResponse>(`/api/admin/users/${userId}/disable`, data),
    enableUser: (userId) => client.post<UserStatusChangeResponse>(`/api/admin/users/${userId}/enable`),
    deleteUser: (userId) => client.delete<void>(`/api/admin/users/${userId}`),
    resetUserPassword: (userId, data) => client.post<PasswordResetResponse>(`/api/admin/users/${userId}/reset-password`, data),
    getUserRoles: async (userId) => {
      const response = await client.get<{ userId: string; roles: RoleListItem[] }>(
        `/api/admin/users/${userId}/roles`
      );
      return normalizeUserRolesResponse(response);
    },
    assignUserRole: (userId, roleId) => client.post<void>(`/api/admin/users/${userId}/roles/${roleId}`),
    unassignUserRole: (userId, roleId) => client.delete<void>(`/api/admin/users/${userId}/roles/${roleId}`),
    getUserGroups: (userId) => client.get<UserGroup[]>(`/api/admin/users/${userId}/groups`),
    getIdentityMigrationInventory: (params) => client.get<IdentityMigrationInventory>("/api/admin/users/identity-migration-inventory", params),
    getUserUpstreamIdentities: async (userId) => {
      const response = await client.get<{ items: UpstreamIdentity[] }>(`/api/admin/users/${userId}/upstream-identities`);
      return normalizeUpstreamIdentityListResponse(response);
    },
    unlinkUserUpstreamIdentity: (userId, providerId) =>
      client.delete<void>(`/api/admin/users/${userId}/upstream-identities/${providerId}`),
  };
}
