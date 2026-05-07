/**
 * useUpdateClient Hook
 * 
 * React Query mutation hook for updating a client
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateClient } from '../api/clients-api';
import { CLIENTS_QUERY_KEY } from './useClients';
import type { Client } from '@/types';

interface UpdateClientParams {
  clientId: string;
  displayName: string;
  description?: string | null;
}

export function useUpdateClient() {
  const queryClient = useQueryClient();

  return useMutation<Client, Error, UpdateClientParams>({
    mutationFn: ({ clientId, ...data }) => updateClient(clientId, data),
    onSuccess: async (data, variables) => {
      // Update the cache for the specific client
      queryClient.setQueryData([CLIENTS_QUERY_KEY, variables.clientId], data);
      // Invalidate the clients list to refetch
      await queryClient.invalidateQueries({ queryKey: [CLIENTS_QUERY_KEY] });
    },
  });
}
