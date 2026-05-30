import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  configureApplicationOAuth,
  updateApplicationMetadata,
} from '../api/applications-api';
import { APPLICATIONS_QUERY_KEY } from './useApplications';
import type {
  Application,
  ConfigureApplicationOAuthRequest,
  UpdateApplicationMetadataRequest,
} from '@/types';

type UpdateApplicationParams =
  | ({ applicationId: string; mode: 'metadata' } & UpdateApplicationMetadataRequest)
  | ({ applicationId: string; mode: 'oauth' } & ConfigureApplicationOAuthRequest);

export function useUpdateApplication() {
  const queryClient = useQueryClient();

  return useMutation<Application, Error, UpdateApplicationParams>({
    mutationFn: (variables) => {
      if (variables.mode === 'metadata') {
        return updateApplicationMetadata(variables.applicationId, {
          displayName: variables.displayName,
          description: variables.description,
        });
      }

      return configureApplicationOAuth(variables.applicationId, {
        profile: variables.profile,
        clientType: variables.clientType,
        allowedGrantTypes: variables.allowedGrantTypes,
        allowedScopes: variables.allowedScopes,
        redirectUris: variables.redirectUris,
        postLogoutRedirectUris: variables.postLogoutRedirectUris,
        requirePkce: variables.requirePkce,
        requireConsent: variables.requireConsent,
      });
    },
    onSuccess: async (data, variables) => {
      queryClient.setQueryData([APPLICATIONS_QUERY_KEY, variables.applicationId], data);
      await queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY] });
    },
  });
}
