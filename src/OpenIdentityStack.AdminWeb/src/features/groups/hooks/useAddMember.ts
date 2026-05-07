import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addMemberToGroup } from '../api/groups-api';

/**
 * Hook to add a member to a group
 * @returns Mutation result
 */
export function useAddMember() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ groupId, userId }: { groupId: string; userId: string }) =>
      addMemberToGroup(groupId, userId),
    onSuccess: async (_, variables) => {
      // Invalidate group members list
      await queryClient.invalidateQueries({
        queryKey: ['groups', variables.groupId, 'members'],
      });
      // Also invalidate the group itself (member count may have changed)
      await queryClient.invalidateQueries({
        queryKey: ['groups', variables.groupId],
      });
    },
  });
}
