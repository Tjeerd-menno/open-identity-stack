/**
 * useDeleteUser Hook
 * 
 * React Query mutation hook for deleting a user
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { deleteUser } from '../api/users-api';
import { USERS_QUERY_KEY } from './useUsers';
import { USER_QUERY_KEY } from './useUser';

export function useDeleteUser() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, string>({
    mutationFn: (userId: string) => deleteUser(userId),
    onSuccess: async (_data, userId) => {
      // Invalidate users list and remove the specific user from cache
      await queryClient.invalidateQueries({ queryKey: [USERS_QUERY_KEY] });
      queryClient.removeQueries({ queryKey: [USER_QUERY_KEY, userId] });
    },
  });
}
