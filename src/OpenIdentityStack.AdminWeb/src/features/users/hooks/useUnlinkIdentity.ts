/**
 * useUnlinkIdentity Hook
 * 
 * React Query mutation hook for unlinking an upstream identity from a user
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { unlinkUpstreamIdentity } from '../api/users-api';
import { UPSTREAM_IDENTITIES_QUERY_KEY } from './useUpstreamIdentities';

interface UnlinkIdentityVariables {
  userId: string;
  providerId: string;
}

export function useUnlinkIdentity() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, UnlinkIdentityVariables>({
    mutationFn: ({ userId, providerId }) => unlinkUpstreamIdentity(userId, providerId),
    onSuccess: async (_data, variables) => {
      // Invalidate upstream identities to refetch
      await queryClient.invalidateQueries({ 
        queryKey: [UPSTREAM_IDENTITIES_QUERY_KEY, variables.userId] 
      });
    },
  });
}
