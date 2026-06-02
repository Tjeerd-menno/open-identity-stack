import { request, type PaginatedResponse } from '@/lib/admin-api';

export type Role = {
  id: string;
  name: string;
  displayName: string;
  description: string | null;
  isSystemRole: boolean;
  isActive: boolean;
  permissions: string[];
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

export function getRoles(params?: RoleListParams): Promise<RoleListResponse> {
  return request<RoleListResponse>('/api/admin/roles', {}, params);
}

export function getRole(roleId: string): Promise<Role> {
  return request<Role>(`/api/admin/roles/${roleId}`);
}

export function createRole(data: CreateRoleRequest): Promise<Role> {
  return request<Role>('/api/admin/roles', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updateRole(roleId: string, data: UpdateRoleRequest): Promise<Role> {
  return request<Role>(`/api/admin/roles/${roleId}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export function deleteRole(roleId: string): Promise<void> {
  return request<void>(`/api/admin/roles/${roleId}`, {
    method: 'DELETE',
  });
}

export function getPlatformPermissionCatalog(): Promise<PlatformPermissionCatalogResponse> {
  return request<PlatformPermissionCatalogResponse>('/api/admin/permissions/platform');
}
