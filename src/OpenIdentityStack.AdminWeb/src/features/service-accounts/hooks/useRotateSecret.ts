/**
 * useRotateSecret Hook
 * 
 * React Query mutation hook for rotating a service account secret
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { rotateSecret } from '../api/service-accounts-api';
import { SERVICE_ACCOUNT_QUERY_KEY } from './useServiceAccount';
import type { RotateSecretRequest, RotateSecretResponse } from '@/types';

interface RotateSecretParams {
  serviceAccountId: string;
  data: RotateSecretRequest;
}

export function useRotateSecret() {
  const queryClient = useQueryClient();

  return useMutation<RotateSecretResponse, Error, RotateSecretParams>({
    mutationFn: ({ serviceAccountId, data }) =>
      rotateSecret(serviceAccountId, data),
    onSuccess: async (_, { serviceAccountId }) => {
      // Invalidate the service account detail query
      await queryClient.invalidateQueries({
        queryKey: [SERVICE_ACCOUNT_QUERY_KEY, serviceAccountId],
      });
    },
  });
}
