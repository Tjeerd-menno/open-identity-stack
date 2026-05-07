/**
 * useClients Hook
 * 
 * React Query hook for fetching paginated list of clients
 */

import { useQuery } from '@tanstack/react-query';
import { getClients } from '../api/clients-api';
import type { PaginationParams, ClientListResponse } from '@/types';

export const CLIENTS_QUERY_KEY = 'clients';

export function useClients(params?: PaginationParams) {
  return useQuery<ClientListResponse>({
    queryKey: [CLIENTS_QUERY_KEY, params],
    queryFn: () => getClients(params),
    staleTime: 30000, // 30 seconds
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
  });
}
