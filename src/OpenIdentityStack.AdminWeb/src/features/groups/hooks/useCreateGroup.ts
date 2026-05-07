import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createGroup } from '../api/groups-api';
import type { CreateGroupRequest } from '@/types';

/**
 * Hook to create a new group
 * @returns Mutation result
 */
export function useCreateGroup() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateGroupRequest) => createGroup(data),
    onSuccess: async () => {
      // Invalidate and refetch groups list
      await queryClient.invalidateQueries({ queryKey: ['groups'] });
    },
  });
}
