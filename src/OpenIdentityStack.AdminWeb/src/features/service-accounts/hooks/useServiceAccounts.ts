/**
 * useServiceAccounts Hook
 * 
 * React Query hook for fetching paginated list of service accounts
 */

import { useQuery } from '@tanstack/react-query';
import { getServiceAccounts } from '../api/service-accounts-api';
import type { PaginationParams, ServiceAccountListResponse } from '@/types';

export const SERVICE_ACCOUNTS_QUERY_KEY = 'service-accounts';

export function useServiceAccounts(params?: PaginationParams) {
  return useQuery<ServiceAccountListResponse>({
    queryKey: [SERVICE_ACCOUNTS_QUERY_KEY, params],
    queryFn: () => getServiceAccounts(params),
    staleTime: 30000, // 30 seconds
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
  });
}
