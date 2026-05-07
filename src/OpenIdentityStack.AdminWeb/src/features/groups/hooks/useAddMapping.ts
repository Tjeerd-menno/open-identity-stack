import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addGroupMapping } from '../api/groups-api';
import type { AddGroupMappingRequest } from '@/types';

/**
 * Hook to add a mapping (role/claim) to a group
 * @returns Mutation result
 */
export function useAddMapping() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      groupId,
      data,
    }: {
      groupId: string;
      data: AddGroupMappingRequest;
    }) => addGroupMapping(groupId, data),
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
