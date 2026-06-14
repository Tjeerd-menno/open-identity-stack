import { adminApiClient } from '@/lib/admin-api';
import {
  createRolesContract,
  type CreateRoleRequest,
  type PlatformPermissionCatalogItem,
  type PlatformPermissionCatalogResponse,
  type Role as SharedRole,
  type RoleListParams,
  type UpdateRoleRequest,
  type PaginatedResponse,
} from '@openidentitystack/admin-api-client';

const contract = createRolesContract(adminApiClient);

export type Role = SharedRole;
export type RoleListItem = Role;
export type RoleListResponse = PaginatedResponse<RoleListItem>;
export type {
  CreateRoleRequest,
  PaginatedResponse,
  PlatformPermissionCatalogItem,
  PlatformPermissionCatalogResponse,
  RoleListParams,
  UpdateRoleRequest,
};

export function getRoles(params?: RoleListParams): Promise<PaginatedResponse<RoleListItem>> {
  return contract.getRoles(params);
}

export function getRole(roleId: string): Promise<Role> {
  return contract.getRole(roleId);
}

export function createRole(data: CreateRoleRequest): Promise<Role> {
  return contract.createRole(data);
}

export function updateRole(roleId: string, data: UpdateRoleRequest): Promise<Role> {
  return contract.updateRole(roleId, data);
}

export function deleteRole(roleId: string): Promise<void> {
  return contract.deleteRole(roleId);
}

export function getPlatformPermissionCatalog(): Promise<PlatformPermissionCatalogResponse> {
  return contract.getPlatformPermissionCatalog();
}
