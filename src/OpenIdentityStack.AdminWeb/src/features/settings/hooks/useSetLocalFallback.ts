import { useMutation, useQueryClient } from '@tanstack/react-query';
import { settingsApi } from '../api';
import type { SetLocalFallbackRequest } from '@/types';

export function useSetLocalFallback() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: SetLocalFallbackRequest) =>
      settingsApi.setLocalFallback(data),
    onSuccess: (updatedSettings) => {
      // Update settings in cache
      queryClient.setQueryData(['authenticationSettings'], updatedSettings);
    }
  });
}
