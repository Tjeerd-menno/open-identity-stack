/**
 * useCreateUser Hook
 * 
 * React Query mutation hook for creating a new user
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createUser } from '../api/users-api';
import { USERS_QUERY_KEY } from './useUsers';
import type { ApiError, CreateUserRequest, User } from '@/types';

export function useCreateUser() {
  const queryClient = useQueryClient();

  return useMutation<User, ApiError, CreateUserRequest>({
    mutationFn: createUser,
    onSuccess: async () => {
      // Invalidate users list to refetch with new user
      await queryClient.invalidateQueries({ queryKey: [USERS_QUERY_KEY] });
    },
  });
}
