/**
 * useUserGroups Hook
 * 
 * React Query hook for fetching groups a user belongs to
 */

import { useQuery } from '@tanstack/react-query';
import { getUserGroups } from '../api/users-api';
import type { Group } from '@/types';

export const USER_GROUPS_QUERY_KEY = 'user-groups';

export function useUserGroups(userId: string) {
  return useQuery<Group[]>({
    queryKey: [USER_GROUPS_QUERY_KEY, userId],
    queryFn: () => getUserGroups(userId),
    enabled: !!userId,
    staleTime: 30000, // 30 seconds
  });
}
