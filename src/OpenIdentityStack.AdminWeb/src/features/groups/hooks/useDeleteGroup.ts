import { useMutation, useQueryClient } from '@tanstack/react-query';
import { deleteGroup } from '../api/groups-api';

/**
 * Hook to delete a group
 * @returns Mutation result
 */
export function useDeleteGroup() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteGroup(id),
    onSuccess: async (_, id) => {
      // Remove the specific group from cache
      queryClient.removeQueries({ queryKey: ['groups', id] });
      // Invalidate and refetch groups list
      await queryClient.invalidateQueries({ queryKey: ['groups'] });
    },
  });
}
