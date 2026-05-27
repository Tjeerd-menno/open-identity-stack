import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addApplicationSecret } from '../api/applications-api';
import { APPLICATIONS_QUERY_KEY } from './useApplications';
import { applicationCredentialsQueryKey } from './useApplicationCredentials';
import type { AddApplicationSecretRequest, AddApplicationSecretResponse } from '@/types';

interface AddApplicationSecretParams {
  applicationId: string;
  data: AddApplicationSecretRequest;
}

export function useAddApplicationSecret() {
  const queryClient = useQueryClient();

  return useMutation<AddApplicationSecretResponse, Error, AddApplicationSecretParams>({
    mutationFn: ({ applicationId, data }) => addApplicationSecret(applicationId, data),
    onSuccess: async (_, { applicationId }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: applicationCredentialsQueryKey(applicationId) }),
        queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY, applicationId] }),
      ]);
    },
  });
}
