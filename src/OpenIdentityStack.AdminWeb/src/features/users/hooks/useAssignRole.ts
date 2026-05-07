/**
 * useAssignRole Hook
 * 
 * React Query mutation hook for assigning a role to a user
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { assignRole } from '../api/users-api';
import { USER_ROLES_QUERY_KEY } from './useUserRoles';

interface AssignRoleVariables {
  userId: string;
  roleId: string;
}

export function useAssignRole() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, AssignRoleVariables>({
    mutationFn: ({ userId, roleId }) => assignRole(userId, roleId),
    onSuccess: async (_data, variables) => {
      // Invalidate user roles to refetch
      await queryClient.invalidateQueries({ 
        queryKey: [USER_ROLES_QUERY_KEY, variables.userId] 
      });
    },
  });
}
