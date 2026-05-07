/**
 * useUserRoles Hook
 * 
 * React Query hook for fetching roles assigned to a user
 */

import { useQuery } from '@tanstack/react-query';
import { getUserRoles } from '../api/users-api';
import type { Role } from '@/types';

export const USER_ROLES_QUERY_KEY = 'user-roles';

export function useUserRoles(userId: string) {
  return useQuery<Role[]>({
    queryKey: [USER_ROLES_QUERY_KEY, userId],
    queryFn: () => getUserRoles(userId),
    enabled: !!userId,
    staleTime: 30000, // 30 seconds
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
  });
}
