import { useMutation, useQueryClient } from '@tanstack/react-query';
import { removeMemberFromGroup } from '../api/groups-api';

/**
 * Hook to remove a member from a group
 * @returns Mutation result
 */
export function useRemoveMember() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ groupId, userId }: { groupId: string; userId: string }) =>
      removeMemberFromGroup(groupId, userId),
    onSuccess: async (_, variables) => {
      // Invalidate group members list
      await queryClient.invalidateQueries({
        queryKey: ['groups', variables.groupId, 'members'],
      });
      // Also invalidate the group itself
      await queryClient.invalidateQueries({
        queryKey: ['groups', variables.groupId],
      });
    },
  });
}
