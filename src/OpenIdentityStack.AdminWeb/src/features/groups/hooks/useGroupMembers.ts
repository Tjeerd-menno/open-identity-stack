import { useQuery } from '@tanstack/react-query';
import { getGroupMembers } from '../api/groups-api';

/**
 * Hook to fetch members of a specific group
 * @param groupId - Group ID
 * @param page - Page number (default: 1)
 * @param pageSize - Number of items per page (default: 20)
 * @returns Query result with group members data
 */
export function useGroupMembers(groupId: string, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: ['groups', groupId, 'members', page, pageSize],
    queryFn: () => getGroupMembers(groupId, page, pageSize),
    enabled: !!groupId,
    staleTime: 30000,
  });
}
