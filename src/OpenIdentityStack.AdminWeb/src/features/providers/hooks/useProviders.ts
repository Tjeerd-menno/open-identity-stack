import { useQuery } from '@tanstack/react-query';
import { providersApi } from '../api';

export function useProviders(includeDisabled: boolean = true) {
  return useQuery({
    queryKey: ['providers', { includeDisabled }],
    queryFn: () => providersApi.getProviders(includeDisabled),
    staleTime: 30000,
    refetchOnMount: 'always' // Always refetch when component mounts for E2E reliability
  });
}
