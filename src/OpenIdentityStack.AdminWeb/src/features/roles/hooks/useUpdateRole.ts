import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateRole } from '../api/roles-api';
import type { UpdateRoleRequest, Role } from '@/types';

/**
 * Mutation hook for updating an existing role
 * 
 * Automatically invalidates related caches on success
 * 
 * @returns Mutation result with mutate function
 * 
 * @example
 * ```tsx
 * const { mutate: update, isPending } = useUpdateRole();
 * 
 * update({
 *   id: roleId,
 *   data: {
 *     displayName: 'Updated Admin',
 *     permissions: ['users:read', 'users:create', 'users:update']
 *   }
 * }, {
 *   onSuccess: () => {
 *     toast.success('Role updated successfully');
 *   }
 * });
 * ```
 */
export function useUpdateRole() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateRoleRequest }) =>
      updateRole(id, data),
    onSuccess: async (updatedRole: Role) => {
      // Invalidate roles list
      await queryClient.invalidateQueries({ queryKey: ['roles'] });
      
      // Update the specific role in cache
      queryClient.setQueryData(['role', updatedRole.id], updatedRole);
      
      // Also invalidate user queries since roles might affect user permissions
      await queryClient.invalidateQueries({ queryKey: ['users'] });
    },
  });
}
