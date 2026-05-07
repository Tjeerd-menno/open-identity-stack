import { useMutation, useQueryClient } from '@tanstack/react-query';
import { providersApi } from '../api';

export function useDeleteProvider() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => providersApi.deleteProvider(id),
    onSuccess: async (_, id) => {
      // Invalidate providers list
      await queryClient.invalidateQueries({ queryKey: ['providers'] });
      // Remove from cache
      queryClient.removeQueries({ queryKey: ['provider', id] });
    }
  });
}
