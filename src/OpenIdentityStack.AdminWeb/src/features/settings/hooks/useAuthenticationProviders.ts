import { useQuery } from '@tanstack/react-query';
import { settingsApi } from '../api';

export function useAuthenticationProviders() {
  return useQuery({
    queryKey: ['authenticationProviders'],
    queryFn: () => settingsApi.getAuthenticationProviders(),
    staleTime: 30000
  });
}
