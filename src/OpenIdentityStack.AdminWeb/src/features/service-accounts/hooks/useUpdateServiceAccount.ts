/**
 * useUpdateServiceAccount Hook
 * 
 * React Query mutation hook for updating a service account
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateServiceAccount } from '../api/service-accounts-api';
import { SERVICE_ACCOUNTS_QUERY_KEY } from './useServiceAccounts';
import { SERVICE_ACCOUNT_QUERY_KEY } from './useServiceAccount';
import type { ServiceAccount, UpdateServiceAccountRequest } from '@/types';

interface UpdateServiceAccountParams {
  serviceAccountId: string;
  data: UpdateServiceAccountRequest;
}

export function useUpdateServiceAccount() {
  const queryClient = useQueryClient();

  return useMutation<ServiceAccount, Error, UpdateServiceAccountParams>({
    mutationFn: ({ serviceAccountId, data }) =>
      updateServiceAccount(serviceAccountId, data),
    onSuccess: async (data) => {
      // Invalidate both the list and the detail query
      await queryClient.invalidateQueries({ queryKey: [SERVICE_ACCOUNTS_QUERY_KEY] });
      await queryClient.invalidateQueries({
        queryKey: [SERVICE_ACCOUNT_QUERY_KEY, data.id],
      });
    },
  });
}
