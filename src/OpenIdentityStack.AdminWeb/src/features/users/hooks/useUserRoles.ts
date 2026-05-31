/**
 * useUserRoles Hook
 * 
 * React Query hook for fetching roles assigned to a user.
 * Requires 'roles:read' permission to execute the query.
 */

import { useQuery } from '@tanstack/react-query';
import { getUserRoles } from '../api/users-api';
import { usePermission } from '@/features/auth/hooks/useAuth';
import type { Role } from '@/types';

export const USER_ROLES_QUERY_KEY = 'user-roles';

export function useUserRoles(userId: string) {
  const hasRolesRead = usePermission('roles:read');

  return useQuery<Role[]>({
    queryKey: [USER_ROLES_QUERY_KEY, userId],
    queryFn: () => getUserRoles(userId),
    enabled: !!userId && hasRolesRead,
    staleTime: 30000, // 30 seconds
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
  });
}
