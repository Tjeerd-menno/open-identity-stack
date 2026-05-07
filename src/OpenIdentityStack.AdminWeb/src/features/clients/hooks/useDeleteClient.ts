/**
 * useDeleteClient Hook
 * 
 * React Query mutation hook for deleting a client
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { deleteClient } from '../api/clients-api';
import { CLIENTS_QUERY_KEY } from './useClients';

export function useDeleteClient() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, string>({
    mutationFn: deleteClient,
    onSuccess: async () => {
      // Invalidate clients list to refetch without deleted client
      await queryClient.invalidateQueries({ queryKey: [CLIENTS_QUERY_KEY] });
    },
  });
}
