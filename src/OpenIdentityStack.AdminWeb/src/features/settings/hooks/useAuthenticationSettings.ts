import { useQuery } from '@tanstack/react-query';
import { settingsApi } from '../api';

export function useAuthenticationSettings() {
  return useQuery({
    queryKey: ['authenticationSettings'],
    queryFn: () => settingsApi.getAuthenticationSettings(),
    staleTime: 30000
  });
}
