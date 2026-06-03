import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getAuthenticationProviders,
  getAuthenticationSettings,
  setDefaultProvider,
  setLocalFallback,
  type AuthenticationProvider,
  type AuthenticationSettings,
  type SetDefaultProviderRequest,
  type SetLocalFallbackRequest,
} from './settings-api';

export const AUTHENTICATION_SETTINGS_QUERY_KEY = 'authentication-settings';
export const AUTHENTICATION_PROVIDERS_QUERY_KEY = 'authentication-providers';

export function useAuthenticationSettings() {
  return useQuery<AuthenticationSettings>({
    queryKey: [AUTHENTICATION_SETTINGS_QUERY_KEY],
    queryFn: getAuthenticationSettings,
    staleTime: 30000,
    refetchOnMount: 'always',
  });
}

export function useAuthenticationProviders() {
  return useQuery<AuthenticationProvider[]>({
    queryKey: [AUTHENTICATION_PROVIDERS_QUERY_KEY],
    queryFn: getAuthenticationProviders,
    staleTime: 30000,
    refetchOnMount: 'always',
  });
}

export function useSetDefaultProvider() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: SetDefaultProviderRequest) => setDefaultProvider(data),
    onSuccess: (settings) => {
      queryClient.setQueryData([AUTHENTICATION_SETTINGS_QUERY_KEY], settings);
      queryClient.invalidateQueries({ queryKey: [AUTHENTICATION_SETTINGS_QUERY_KEY] });
    },
  });
}

export function useSetLocalFallback() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: SetLocalFallbackRequest) => setLocalFallback(data),
    onSuccess: (settings) => {
      queryClient.setQueryData([AUTHENTICATION_SETTINGS_QUERY_KEY], settings);
      queryClient.invalidateQueries({ queryKey: [AUTHENTICATION_SETTINGS_QUERY_KEY] });
    },
  });
}
