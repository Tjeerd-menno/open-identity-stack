import { useQuery } from '@tanstack/react-query';
import { providersApi } from '../api';

export function useProvider(id: string) {
  return useQuery({
    queryKey: ['provider', id],
    queryFn: () => providersApi.getProvider(id),
    enabled: !!id,
    staleTime: 30000,
    refetchOnMount: 'always' // Always refetch when component mounts for E2E reliability
  });
}
