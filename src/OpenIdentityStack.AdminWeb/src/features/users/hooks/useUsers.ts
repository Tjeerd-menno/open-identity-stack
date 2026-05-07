/**
 * useUsers Hook
 * 
 * React Query hook for fetching paginated list of users
 */

import { useQuery } from '@tanstack/react-query';
import { getUsers } from '../api/users-api';
import type { PaginationParams, UserListResponse } from '@/types';

export const USERS_QUERY_KEY = 'users';

export interface UseUsersOptions extends PaginationParams {
  /** Whether the query should run. Defaults to true. */
  enabled?: boolean;
}

export function useUsers(options: UseUsersOptions = {}) {
  const { enabled = true, ...params } = options;
  return useQuery<UserListResponse>({
    queryKey: [USERS_QUERY_KEY, params],
    queryFn: () => getUsers(params),
    staleTime: 30000, // 30 seconds
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
    enabled,
  });
}
