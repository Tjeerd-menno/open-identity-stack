import type { AdminApiClient, PaginatedResponse } from './index';

export type Role = {
  id: string;
  name: string;
  displayName: string;
  isSystemRole: boolean;
  isActive: boolean;
  permissions: string[];
  description?: string | null;
};

export type RoleListItem = Role;

export type RoleListParams = {
  page?: number;
  pageSize?: number;
  search?: string;
};

export type RoleListResponse = PaginatedResponse<RoleListItem>;

export type CreateRoleRequest = {
  name: string;
  displayName: string;
  description?: string | null;
  permissions: string[];
  acknowledgeWildcardGrant: boolean;
};

export type UpdateRoleRequest = {
  displayName: string;
  description?: string | null;
  permissions: string[];
  acknowledgeWildcardGrant: boolean;
};

export type PlatformPermissionCatalogItem = {
  permission: string;
  resource: string;
  action: string;
  kind: 'concrete' | 'wildcard';
  displayName: string;
  assignable: boolean;
};

export type PlatformPermissionCatalogResponse = {
  items: PlatformPermissionCatalogItem[];
};

export type RolesContract = {
  getRoles: (params?: RoleListParams) => Promise<RoleListResponse>;
  getRole: (roleId: string) => Promise<Role>;
  createRole: (data: CreateRoleRequest) => Promise<Role>;
  updateRole: (roleId: string, data: UpdateRoleRequest) => Promise<Role>;
  deleteRole: (roleId: string) => Promise<void>;
  getPlatformPermissionCatalog: () => Promise<PlatformPermissionCatalogResponse>;
};

export function createRolesContract(client: AdminApiClient): RolesContract {
  return {
    getRoles: (params) => client.get<RoleListResponse>('/api/admin/roles', params),
    getRole: (roleId) => client.get<Role>(`/api/admin/roles/${roleId}`),
    createRole: (data) => client.post<Role>('/api/admin/roles', data),
    updateRole: (roleId, data) => client.put<Role>(`/api/admin/roles/${roleId}`, data),
    deleteRole: (roleId) => client.delete<void>(`/api/admin/roles/${roleId}`),
    getPlatformPermissionCatalog: () => client.get<PlatformPermissionCatalogResponse>('/api/admin/permissions/platform'),
  };
}
