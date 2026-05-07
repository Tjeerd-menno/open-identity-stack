/**
 * useClient Hook
 * 
 * React Query hook for fetching a single client by ID
 */

import { useQuery } from '@tanstack/react-query';
import { getClient } from '../api/clients-api';
import { CLIENTS_QUERY_KEY } from './useClients';
import type { Client } from '@/types';

export function useClient(clientId: string) {
  return useQuery<Client>({
    queryKey: [CLIENTS_QUERY_KEY, clientId],
    queryFn: () => getClient(clientId),
    staleTime: 30000, // 30 seconds
    enabled: !!clientId,
  });
}
