import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addApplicationCertificate } from '../api/applications-api';
import { APPLICATIONS_QUERY_KEY } from './useApplications';
import { applicationCredentialsQueryKey } from './useApplicationCredentials';
import type { AddApplicationCertificateRequest, AddApplicationCertificateResponse } from '@/types';

interface AddApplicationCertificateParams {
  applicationId: string;
  data: AddApplicationCertificateRequest;
}

export function useAddApplicationCertificate() {
  const queryClient = useQueryClient();

  return useMutation<AddApplicationCertificateResponse, Error, AddApplicationCertificateParams>({
    mutationFn: ({ applicationId, data }) => addApplicationCertificate(applicationId, data),
    onSuccess: async (_, { applicationId }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: applicationCredentialsQueryKey(applicationId) }),
        queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY, applicationId] }),
      ]);
    },
  });
}
