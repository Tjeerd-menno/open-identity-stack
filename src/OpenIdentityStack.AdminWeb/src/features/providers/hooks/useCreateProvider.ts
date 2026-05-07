import { useMutation, useQueryClient } from '@tanstack/react-query';
import { providersApi } from '../api';
import type { CreateProviderRequest } from '@/types';

export function useCreateProvider() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateProviderRequest) => providersApi.createProvider(data),
    onSuccess: async (newProvider) => {
      // Invalidate providers list
      await queryClient.invalidateQueries({ queryKey: ['providers'] });
      // Set the new provider in cache
      queryClient.setQueryData(['provider', newProvider.id], newProvider);
    }
  });
}
