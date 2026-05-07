import { useMutation, useQueryClient } from '@tanstack/react-query';
import { removeGroupMapping } from '../api/groups-api';

/**
 * Hook to remove a mapping from a group
 * @returns Mutation result
 */
export function useRemoveMapping() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      groupId,
      mappingId,
    }: {
      groupId: string;
      mappingId: string;
    }) => removeGroupMapping(groupId, mappingId),
    onSuccess: async (_, variables) => {
      // Invalidate group mappings list
      await queryClient.invalidateQueries({
        queryKey: ['groups', variables.groupId, 'mappings'],
      });
      // Also invalidate the group itself
      await queryClient.invalidateQueries({
        queryKey: ['groups', variables.groupId],
      });
    },
  });
}
