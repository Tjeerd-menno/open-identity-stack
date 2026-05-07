import { useMutation, useQueryClient } from '@tanstack/react-query';
import { revokeSession } from '../api/sessions-api';

export function useRevokeSession() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: revokeSession,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['sessions'] });
    },
  });
}
