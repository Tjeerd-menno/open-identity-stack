/**
 * useUpstreamIdentities Hook
 * 
 * React Query hook for fetching upstream identities linked to a user
 */

import { useQuery } from '@tanstack/react-query';
import { getUpstreamIdentities } from '../api/users-api';
import type { UpstreamIdentity } from '@/types';

export const UPSTREAM_IDENTITIES_QUERY_KEY = 'upstream-identities';

export function useUpstreamIdentities(userId: string) {
  return useQuery<UpstreamIdentity[]>({
    queryKey: [UPSTREAM_IDENTITIES_QUERY_KEY, userId],
    queryFn: () => getUpstreamIdentities(userId),
    enabled: !!userId,
    staleTime: 30000, // 30 seconds
  });
}
