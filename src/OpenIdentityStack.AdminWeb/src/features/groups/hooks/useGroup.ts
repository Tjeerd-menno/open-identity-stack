import { useQuery } from '@tanstack/react-query';
import { getGroup } from '../api/groups-api';

/**
 * Hook to fetch details of a specific group
 * @param id - Group ID
 * @returns Query result with group data
 */
export function useGroup(id: string) {
  return useQuery({
    queryKey: ['groups', id],
    queryFn: () => getGroup(id),
    enabled: !!id, // Only fetch if ID is provided
    staleTime: 30000,
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
  });
}
