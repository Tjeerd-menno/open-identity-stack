import { apiClient } from '@/lib/api/client';
import type {
  Role,
  RoleListItem,
  CreateRoleRequest,
  UpdateRoleRequest,
  PaginatedResponse,
  PaginationParams,
} from '@/types';

/**
 * Roles API Client
 * 
 * Provides methods to interact with the /api/admin/roles endpoints.
 * All endpoints require authentication and appropriate permissions.
 */

const BASE_PATH = '/api/admin/roles';

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
  const { page = 1, pageSize = 20, search } = params;
  
  const response = await apiClient.get<PaginatedResponse<RoleListItem>>(BASE_PATH, {
    page,
    pageSize,
    ...(search && { search }),
  });
  
  return response;
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
  const response = await apiClient.get<Role>(`${BASE_PATH}/${id}`);
  return response;
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
  const response = await apiClient.post<Role>(BASE_PATH, data);
  return response;
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
  const response = await apiClient.put<Role>(`${BASE_PATH}/${id}`, data);
  return response;
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
  await apiClient.delete(`${BASE_PATH}/${id}`);
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
  const builtInPermissions = [
    // User permissions
    'users:read',
    'users:create',
    'users:update',
    'users:delete',
    
    // Role permissions
    'roles:read',
    'roles:create',
    'roles:update',
    'roles:delete',
    
    // Group permissions
    'groups:read',
    'groups:create',
    'groups:update',
    'groups:delete',
    
    // Service account permissions
    'service-accounts:read',
    'service-accounts:create',
    'service-accounts:update',
    'service-accounts:delete',
    
    // Session permissions
    'sessions:read',
    'sessions:revoke',
    
    // Provider permissions
    'providers:read',
    'providers:create',
    'providers:update',
    'providers:delete',
  ];

  try {
    const catalog = await apiClient.get<{ items: { fullPermissionKey: string }[] }>('/api/admin/service-permissions/catalog', {
      page: 1,
      pageSize: 100,
      assignableOnly: true,
    });
    return Array.from(new Set([...builtInPermissions, ...catalog.items.map((item) => item.fullPermissionKey)]));
  } catch {
    return builtInPermissions;
  }
}
