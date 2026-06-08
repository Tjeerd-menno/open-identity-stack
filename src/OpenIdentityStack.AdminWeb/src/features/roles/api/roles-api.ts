import { adminApiClient } from '@/lib/api/client';
import type {
  Role,
  RoleListItem,
  CreateRoleRequest,
  UpdateRoleRequest,
  PlatformPermissionCatalogResponse,
  PaginatedResponse,
  PaginationParams,
} from '@/types';
import { createRolesContract } from '@openidentitystack/admin-api-client';

/**
 * Roles API Client
 * 
 * Provides methods to interact with the /api/admin/roles endpoints.
 * All endpoints require authentication and appropriate permissions.
 */

const contract = createRolesContract(adminApiClient);

const builtInPermissions: string[] = [
  'users:read',
  'users:create',
  'users:update',
  'users:delete',
  'roles:read',
  'roles:create',
  'roles:update',
  'roles:delete',
  'groups:read',
  'groups:create',
  'groups:update',
  'groups:delete',
  'service-accounts:read',
  'service-accounts:create',
  'service-accounts:update',
  'service-accounts:delete',
  'sessions:read',
  'sessions:revoke',
  'providers:read',
  'providers:create',
  'providers:update',
  'providers:delete',
];

/**
 * Fetch paginated list of roles
 * 
 * @param params - Pagination parameters
 * @returns Paginated response with role list items
 * 
 * Required permission: roles:read
 */
export async function getRoles(
  params: PaginationParams = {}
): Promise<PaginatedResponse<RoleListItem>> {
  const requestParams = {
    page: params.page ?? 1,
    pageSize: params.pageSize ?? 20,
    ...(params.search ? { search: params.search } : {}),
  };
  return contract.getRoles(requestParams as { page?: number; pageSize?: number; search?: string }) as Promise<
    PaginatedResponse<RoleListItem>
  >;
}

/**
 * Fetch a single role by ID
 * 
 * @param id - Role ID (GUID)
 * @returns Full role details including permissions
 * 
 * Required permission: roles:read
 */
export async function getRole(id: string): Promise<Role> {
  return contract.getRole(id) as Promise<Role>;
}

/**
 * Create a new role
 * 
 * @param data - Role creation data
 * @returns Created role with assigned ID
 * 
 * Required permission: roles:create
 */
export async function createRole(data: CreateRoleRequest): Promise<Role> {
  return contract.createRole(data) as Promise<Role>;
}

/**
 * Update an existing role
 * 
 * @param id - Role ID to update
 * @param data - Partial role data to update
 * @returns Updated role details
 * 
 * Required permission: roles:update
 */
export async function updateRole(
  id: string,
  data: UpdateRoleRequest
): Promise<Role> {
  return contract.updateRole(id, data) as Promise<Role>;
}

/**
 * Delete a role
 * 
 * Note: System roles cannot be deleted and will return an error
 * 
 * @param id - Role ID to delete
 * 
 * Required permission: roles:delete
 */
export async function deleteRole(id: string): Promise<void> {
  return contract.deleteRole(id);
}

/**
 * Get available permissions list
 * 
 * This is a utility function to fetch all available permissions
 * that can be assigned to roles. The actual endpoint may vary
 * based on your API implementation.
 * 
 * @returns Array of permission strings
 */
export async function getAvailablePermissions(): Promise<string[]> {
  try {
    const catalog = await contract.getPlatformPermissionCatalog() as unknown as {
      items: Array<{ fullPermissionKey?: string; permission?: string; resource?: string; action?: string }>;
    };
    const values = catalog.items.map((item) =>
      item.fullPermissionKey ??
      (item.permission ?? (item.resource && item.action ? `${item.resource}:${item.action}` : undefined)) ??
      ''
    );
    return Array.from(new Set([...builtInPermissions, ...values.filter(Boolean)]));
  } catch {
    return builtInPermissions;
  }
}

export async function getPlatformPermissionCatalog(): Promise<PlatformPermissionCatalogResponse> {
  return contract.getPlatformPermissionCatalog() as Promise<PlatformPermissionCatalogResponse>;
}
