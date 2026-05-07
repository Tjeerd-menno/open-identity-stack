import { useMutation, useQueryClient } from '@tanstack/react-query';
import { providersApi } from '../api';
import type { UpdateProviderRequest } from '@/types';

export function useUpdateProvider() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateProviderRequest }) =>
      providersApi.updateProvider(id, data),
    onSuccess: async (updatedProvider, variables) => {
      // Invalidate providers list
      await queryClient.invalidateQueries({ queryKey: ['providers'] });
      // Update the provider in cache
      queryClient.setQueryData(['provider', variables.id], updatedProvider);
    }
  });
}
