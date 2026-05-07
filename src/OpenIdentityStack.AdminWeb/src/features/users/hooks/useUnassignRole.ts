/**
 * useUnassignRole Hook
 * 
 * React Query mutation hook for unassigning a role from a user
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { unassignRole } from '../api/users-api';
import { USER_ROLES_QUERY_KEY } from './useUserRoles';

interface UnassignRoleVariables {
  userId: string;
  roleId: string;
}

export function useUnassignRole() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, UnassignRoleVariables>({
    mutationFn: ({ userId, roleId }) => unassignRole(userId, roleId),
    onSuccess: async (_data, variables) => {
      // Invalidate user roles to refetch
      await queryClient.invalidateQueries({ 
        queryKey: [USER_ROLES_QUERY_KEY, variables.userId] 
      });
    },
  });
}
