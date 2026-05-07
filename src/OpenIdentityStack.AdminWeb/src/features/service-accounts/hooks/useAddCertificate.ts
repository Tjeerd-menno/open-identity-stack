/**
 * useAddCertificate Hook
 * 
 * React Query mutation hook for adding a certificate to a service account
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addCertificate } from '../api/service-accounts-api';
import { SERVICE_ACCOUNT_QUERY_KEY } from './useServiceAccount';
import type { AddCertificateRequest, AddCertificateResponse } from '@/types';

interface AddCertificateParams {
  serviceAccountId: string;
  data: AddCertificateRequest;
}

export function useAddCertificate() {
  const queryClient = useQueryClient();

  return useMutation<AddCertificateResponse, Error, AddCertificateParams>({
    mutationFn: ({ serviceAccountId, data }) =>
      addCertificate(serviceAccountId, data),
    onSuccess: async (_, { serviceAccountId }) => {
      // Invalidate the service account detail query
      await queryClient.invalidateQueries({
        queryKey: [SERVICE_ACCOUNT_QUERY_KEY, serviceAccountId],
      });
    },
  });
}
