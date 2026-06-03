import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  createRole,
  deleteRole,
  getPlatformPermissionCatalog,
  getRole,
  getRoles,
  updateRole,
  type CreateRoleRequest,
  type PlatformPermissionCatalogResponse,
  type Role,
  type RoleListParams,
  type RoleListResponse,
  type UpdateRoleRequest,
} from './roles-api';

export const ROLES_QUERY_KEY = 'roles';

export function useRoles(params?: RoleListParams) {
  return useQuery<RoleListResponse>({
    queryKey: [ROLES_QUERY_KEY, params],
    queryFn: () => getRoles(params),
    staleTime: 30000,
    refetchOnMount: 'always',
  });
}

export function useRole(roleId: string | undefined) {
  return useQuery<Role>({
    queryKey: [ROLES_QUERY_KEY, roleId],
    queryFn: () => getRole(roleId ?? ''),
    enabled: !!roleId,
  });
}

export function usePlatformPermissionCatalog() {
  return useQuery<PlatformPermissionCatalogResponse>({
    queryKey: [ROLES_QUERY_KEY, 'platform-permissions'],
    queryFn: getPlatformPermissionCatalog,
    staleTime: 60000,
  });
}

export function useCreateRole() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateRoleRequest) => createRole(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [ROLES_QUERY_KEY] }),
  });
}

export function useUpdateRole(roleId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateRoleRequest) => updateRole(roleId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [ROLES_QUERY_KEY] });
      queryClient.invalidateQueries({ queryKey: [ROLES_QUERY_KEY, roleId] });
    },
  });
}

export function useDeleteRole(roleId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => deleteRole(roleId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [ROLES_QUERY_KEY] }),
  });
}
