import { useQuery } from '@tanstack/react-query';
import { getGroupMappings } from '../api/groups-api';

/**
 * Hook to fetch mappings (roles/claims) for a specific group
 * @param groupId - Group ID
 * @returns Query result with group mappings data
 */
export function useGroupMappings(groupId: string) {
  return useQuery({
    queryKey: ['groups', groupId, 'mappings'],
    queryFn: () => getGroupMappings(groupId),
    enabled: !!groupId,
    staleTime: 30000,
  });
}
