import { useQuery } from '@tanstack/react-query';
import { getGroups } from '../api/groups-api';

export interface UseGroupsOptions {
  page?: number;
  pageSize?: number;
  search?: string;
  /** Whether the query should run. Defaults to true. */
  enabled?: boolean;
}

/**
 * Hook to fetch paginated list of groups
 * @param options - Pagination, search, and query options
 * @returns Query result with groups data
 */
export function useGroups(options: UseGroupsOptions = {}) {
  const { page = 1, pageSize = 20, search, enabled = true } = options;
  return useQuery({
    queryKey: ['groups', page, pageSize, search],
    queryFn: () => getGroups(page, pageSize, search),
    staleTime: 30000, // Consider data fresh for 30 seconds
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
    enabled,
  });
}
