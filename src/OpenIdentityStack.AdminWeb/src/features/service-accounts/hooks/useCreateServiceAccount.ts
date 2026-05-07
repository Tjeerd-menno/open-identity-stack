/**
 * useCreateServiceAccount Hook
 * 
 * React Query mutation hook for creating a new service account
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createServiceAccount } from '../api/service-accounts-api';
import { SERVICE_ACCOUNTS_QUERY_KEY } from './useServiceAccounts';
import type { CreateServiceAccountRequest, ServiceAccountCreatedResponse } from '@/types';

export function useCreateServiceAccount() {
  const queryClient = useQueryClient();

  return useMutation<ServiceAccountCreatedResponse, Error, CreateServiceAccountRequest>({
    mutationFn: createServiceAccount,
    onSuccess: async () => {
      // Invalidate service accounts list to refetch with new service account
      await queryClient.invalidateQueries({ queryKey: [SERVICE_ACCOUNTS_QUERY_KEY] });
    },
  });
}
