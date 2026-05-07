import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateGroup } from '../api/groups-api';
import type { UpdateGroupRequest } from '@/types';

/**
 * Hook to update an existing group
 * @returns Mutation result
 */
export function useUpdateGroup() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateGroupRequest }) =>
      updateGroup(id, data),
    onSuccess: async (_, variables) => {
      // Invalidate the specific group and groups list
      await queryClient.invalidateQueries({ queryKey: ['groups', variables.id] });
      await queryClient.invalidateQueries({ queryKey: ['groups'] });
    },
  });
}
