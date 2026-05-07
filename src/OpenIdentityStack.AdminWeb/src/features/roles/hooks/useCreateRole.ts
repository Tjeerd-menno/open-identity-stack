import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createRole } from '../api/roles-api';
import type { CreateRoleRequest, Role } from '@/types';

/**
 * Mutation hook for creating a new role
 * 
 * Automatically invalidates roles list cache on success
 * 
 * @returns Mutation result with mutate function
 * 
 * @example
 * ```tsx
 * const { mutate: createNewRole, isPending } = useCreateRole();
 * 
 * createNewRole({
 *   name: 'custom-admin',
 *   displayName: 'Custom Admin',
 *   permissions: ['users:read', 'users:create']
 * }, {
 *   onSuccess: (role) => {
 *     console.log('Created role:', role);
 *   }
 * });
 * ```
 */
export function useCreateRole() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateRoleRequest) => createRole(data),
    onSuccess: async (newRole: Role) => {
      // Invalidate roles list to refetch
      await queryClient.invalidateQueries({ queryKey: ['roles'] });
      
      // Optionally, set the new role in cache
      queryClient.setQueryData(['role', newRole.id], newRole);
    },
  });
}
