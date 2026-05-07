/**
 * useDeleteServiceAccount Hook
 * 
 * React Query mutation hook for deleting a service account
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { deleteServiceAccount } from '../api/service-accounts-api';
import { SERVICE_ACCOUNTS_QUERY_KEY } from './useServiceAccounts';

export function useDeleteServiceAccount() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, string>({
    mutationFn: deleteServiceAccount,
    onSuccess: async () => {
      // Invalidate service accounts list to refetch
      await queryClient.invalidateQueries({ queryKey: [SERVICE_ACCOUNTS_QUERY_KEY] });
    },
  });
}
