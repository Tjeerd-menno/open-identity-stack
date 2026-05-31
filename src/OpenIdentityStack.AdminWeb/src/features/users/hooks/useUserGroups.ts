/**
 * useUserGroups Hook
 * 
 * React Query hook for fetching groups a user belongs to.
 * Requires 'groups:read' permission to execute the query.
 */

import { useQuery } from '@tanstack/react-query';
import { getUserGroups } from '../api/users-api';
import { usePermission } from '@/features/auth/hooks/useAuth';
import type { Group } from '@/types';

export const USER_GROUPS_QUERY_KEY = 'user-groups';

export function useUserGroups(userId: string) {
  const hasGroupsRead = usePermission('groups:read');

  return useQuery<Group[]>({
    queryKey: [USER_GROUPS_QUERY_KEY, userId],
    queryFn: () => getUserGroups(userId),
    enabled: !!userId && hasGroupsRead,
    staleTime: 30000, // 30 seconds
  });
}
