import { useQuery } from '@tanstack/react-query';
import { getRole } from '../api/roles-api';

/**
 * Query hook for fetching a single role by ID
 * 
 * @param id - Role ID (GUID)
 * @param enabled - Whether to enable the query (default: true if id exists)
 * @returns Query result with role details including permissions
 * 
 * @example
 * ```tsx
 * const { data: role, isLoading } = useRole(roleId);
 * ```
 */
export function useRole(id: string | undefined, enabled = true) {
  return useQuery({
    queryKey: ['role', id],
    queryFn: () => getRole(id!),
    enabled: enabled && !!id,
    staleTime: 5 * 60 * 1000, // 5 minutes
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
  });
}
