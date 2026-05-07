import { useMutation, useQueryClient } from '@tanstack/react-query';
import { deleteRole } from '../api/roles-api';

/**
 * Mutation hook for deleting a role
 * 
 * Note: System roles cannot be deleted and will return an error
 * Automatically invalidates roles list cache on success
 * 
 * @returns Mutation result with mutate function
 * 
 * @example
 * ```tsx
 * const { mutate: remove, isPending } = useDeleteRole();
 * 
 * remove(roleId, {
 *   onSuccess: () => {
 *     toast.success('Role deleted successfully');
 *     navigate('/roles');
 *   },
 *   onError: (error) => {
 *     toast.error('Cannot delete system role');
 *   }
 * });
 * ```
 */
export function useDeleteRole() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteRole(id),
    onSuccess: async (_, deletedId) => {
      // Invalidate roles list
      await queryClient.invalidateQueries({ queryKey: ['roles'] });
      
      // Remove the specific role from cache
      queryClient.removeQueries({ queryKey: ['role', deletedId] });
      
      // Invalidate users since role assignments might be affected
      await queryClient.invalidateQueries({ queryKey: ['users'] });
    },
  });
}
