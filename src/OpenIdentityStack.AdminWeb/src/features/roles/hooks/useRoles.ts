import { useQuery } from '@tanstack/react-query';
import { getRoles } from '../api/roles-api';
import type { PaginationParams } from '@/types';

export interface UseRolesOptions extends PaginationParams {
  /** Whether the query should run. Defaults to true. */
  enabled?: boolean;
}

/**
 * Query hook for fetching paginated roles list
 * 
 * @param options - Pagination, search, and query options
 * @returns Query result with roles list and pagination info
 * 
 * @example
 * ```tsx
 * const { data, isLoading, error } = useRoles({ page: 1, pageSize: 20, enabled: isAuthenticated });
 * ```
 */
export function useRoles(options: UseRolesOptions = {}) {
  const { enabled = true, ...params } = options;
  return useQuery({
    queryKey: ['roles', params],
    queryFn: () => getRoles(params),
    staleTime: 5 * 60 * 1000, // 5 minutes
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
    enabled,
  });
}
