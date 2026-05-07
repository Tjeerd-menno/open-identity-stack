/**
 * useCreateClient Hook
 * 
 * React Query mutation hook for creating a new client
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createClient } from '../api/clients-api';
import { CLIENTS_QUERY_KEY } from './useClients';
import type { CreateClientRequest, ClientCreatedResponse } from '@/types';

export function useCreateClient() {
  const queryClient = useQueryClient();

  return useMutation<ClientCreatedResponse, Error, CreateClientRequest>({
    mutationFn: createClient,
    onSuccess: async () => {
      // Invalidate clients list to refetch with new client
      await queryClient.invalidateQueries({ queryKey: [CLIENTS_QUERY_KEY] });
    },
  });
}
