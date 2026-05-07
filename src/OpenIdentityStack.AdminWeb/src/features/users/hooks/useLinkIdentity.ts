/**
 * useLinkIdentity Hook
 * 
 * React Query mutation hook for linking an upstream identity to a user
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { linkUpstreamIdentity } from '../api/users-api';
import { UPSTREAM_IDENTITIES_QUERY_KEY } from './useUpstreamIdentities';
import type { LinkUpstreamIdentityRequest, LinkUpstreamIdentityResponse } from '@/types';

interface LinkIdentityVariables {
  userId: string;
  data: LinkUpstreamIdentityRequest;
}

export function useLinkIdentity() {
  const queryClient = useQueryClient();

  return useMutation<LinkUpstreamIdentityResponse, Error, LinkIdentityVariables>({
    mutationFn: ({ userId, data }) => linkUpstreamIdentity(userId, data),
    onSuccess: async (_data, variables) => {
      // Invalidate upstream identities to refetch
      await queryClient.invalidateQueries({ 
        queryKey: [UPSTREAM_IDENTITIES_QUERY_KEY, variables.userId] 
      });
    },
  });
}
