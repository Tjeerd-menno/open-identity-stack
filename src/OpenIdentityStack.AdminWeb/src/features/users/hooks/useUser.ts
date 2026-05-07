/**
 * useUser Hook
 * 
 * React Query hook for fetching a single user by ID
 */

import { useQuery } from '@tanstack/react-query';
import { getUser } from '../api/users-api';
import type { User } from '@/types';

export const USER_QUERY_KEY = 'user';

export function useUser(userId: string) {
  return useQuery<User>({
    queryKey: [USER_QUERY_KEY, userId],
    queryFn: () => getUser(userId),
    enabled: !!userId,
    staleTime: 30000, // 30 seconds
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
  });
}
