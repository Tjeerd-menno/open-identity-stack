/**
 * useDisableServiceAccount Hook
 * 
 * React Query mutation hook for disabling a service account
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { disableServiceAccount } from '../api/service-accounts-api';
import { SERVICE_ACCOUNTS_QUERY_KEY } from './useServiceAccounts';
import { SERVICE_ACCOUNT_QUERY_KEY } from './useServiceAccount';

export function useDisableServiceAccount() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, string>({
    mutationFn: disableServiceAccount,
    onSuccess: async (_, serviceAccountId) => {
      // Invalidate both the list and the detail query
      await queryClient.invalidateQueries({ queryKey: [SERVICE_ACCOUNTS_QUERY_KEY] });
      await queryClient.invalidateQueries({
        queryKey: [SERVICE_ACCOUNT_QUERY_KEY, serviceAccountId],
      });
    },
  });
}
