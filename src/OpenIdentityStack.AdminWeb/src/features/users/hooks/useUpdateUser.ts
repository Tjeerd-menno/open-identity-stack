/**
 * useUpdateUser Hook
 * 
 * React Query mutation hook for updating a user
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateUser } from '../api/users-api';
import { USERS_QUERY_KEY } from './useUsers';
import { USER_QUERY_KEY } from './useUser';
import type { UpdateUserRequest, User } from '@/types';

interface UpdateUserVariables {
  userId: string;
  data: UpdateUserRequest;
}

export function useUpdateUser() {
  const queryClient = useQueryClient();

  return useMutation<User, Error, UpdateUserVariables>({
    mutationFn: ({ userId, data }) => updateUser(userId, data),
    onSuccess: async (_data, variables) => {
      // Invalidate both the users list and the specific user
      await queryClient.invalidateQueries({ queryKey: [USERS_QUERY_KEY] });
      await queryClient.invalidateQueries({ queryKey: [USER_QUERY_KEY, variables.userId] });
    },
  });
}
