/**
 * useEnableUser Hook
 * 
 * React Query mutation hook for enabling a user
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { enableUser } from '../api/users-api';
import { USERS_QUERY_KEY } from './useUsers';
import { USER_QUERY_KEY } from './useUser';
import type { UserStatusChangeResponse } from '@/types';

export function useEnableUser() {
  const queryClient = useQueryClient();

  return useMutation<UserStatusChangeResponse, Error, string>({
    mutationFn: (userId: string) => enableUser(userId),
    onSuccess: async (_data, userId) => {
      // Invalidate both the users list and the specific user
      await queryClient.invalidateQueries({ queryKey: [USERS_QUERY_KEY] });
      await queryClient.invalidateQueries({ queryKey: [USER_QUERY_KEY, userId] });
    },
  });
}
