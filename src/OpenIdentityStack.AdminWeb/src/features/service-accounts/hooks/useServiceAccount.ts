/**
 * useServiceAccount Hook
 * 
 * React Query hook for fetching a single service account by ID
 */

import { useQuery } from '@tanstack/react-query';
import { getServiceAccount } from '../api/service-accounts-api';
import type { ServiceAccount } from '@/types';

export const SERVICE_ACCOUNT_QUERY_KEY = 'service-account';

export function useServiceAccount(serviceAccountId: string) {
  return useQuery<ServiceAccount>({
    queryKey: [SERVICE_ACCOUNT_QUERY_KEY, serviceAccountId],
    queryFn: () => getServiceAccount(serviceAccountId),
    staleTime: 30000, // 30 seconds
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
    enabled: !!serviceAccountId,
  });
}
