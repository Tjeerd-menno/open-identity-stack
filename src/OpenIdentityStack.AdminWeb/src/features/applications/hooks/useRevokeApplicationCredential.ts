import { useMutation, useQueryClient } from '@tanstack/react-query';
import { revokeApplicationCredential } from '../api/applications-api';
import { APPLICATIONS_QUERY_KEY } from './useApplications';
import { applicationCredentialsQueryKey } from './useApplicationCredentials';

interface RevokeApplicationCredentialParams {
  applicationId: string;
  credentialId: string;
}

export function useRevokeApplicationCredential() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, RevokeApplicationCredentialParams>({
    mutationFn: ({ applicationId, credentialId }) =>
      revokeApplicationCredential(applicationId, credentialId),
    onSuccess: async (_, { applicationId }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: applicationCredentialsQueryKey(applicationId) }),
        queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY, applicationId] }),
      ]);
    },
  });
}
