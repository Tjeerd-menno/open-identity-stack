import { useMutation, useQueryClient } from '@tanstack/react-query';
import { settingsApi } from '../api';
import type { SetDefaultProviderRequest } from '@/types';

export function useSetDefaultProvider() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: SetDefaultProviderRequest) =>
      settingsApi.setDefaultProvider(data),
    onSuccess: (updatedSettings) => {
      // Update settings in cache
      queryClient.setQueryData(['authenticationSettings'], updatedSettings);
      // Also invalidate providers list as default may have changed
      queryClient.invalidateQueries({ queryKey: ['authenticationProviders'] });
    }
  });
}
