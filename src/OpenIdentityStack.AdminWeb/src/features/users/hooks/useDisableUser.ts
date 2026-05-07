/**
 * useDisableUser Hook
 * 
 * React Query mutation hook for disabling a user
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { disableUser } from '../api/users-api';
import { USERS_QUERY_KEY } from './useUsers';
import { USER_QUERY_KEY } from './useUser';
import type { DisableUserRequest, UserStatusChangeResponse } from '@/types';

interface DisableUserVariables {
  userId: string;
  data: DisableUserRequest;
}

export function useDisableUser() {
  const queryClient = useQueryClient();

  return useMutation<UserStatusChangeResponse, Error, DisableUserVariables>({
    mutationFn: ({ userId, data }) => disableUser(userId, data),
    onSuccess: async (_data, variables) => {
      // Invalidate both the users list and the specific user
      await queryClient.invalidateQueries({ queryKey: [USERS_QUERY_KEY] });
      await queryClient.invalidateQueries({ queryKey: [USER_QUERY_KEY, variables.userId] });
    },
  });
}
