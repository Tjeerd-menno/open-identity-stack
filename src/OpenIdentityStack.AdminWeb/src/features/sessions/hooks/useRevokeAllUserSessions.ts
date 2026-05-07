import { useMutation, useQueryClient } from '@tanstack/react-query';
import { revokeAllUserSessions } from '../api/sessions-api';

export function useRevokeAllUserSessions() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: revokeAllUserSessions,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['sessions'] });
      await queryClient.invalidateQueries({ queryKey: ['users'] });
    },
  });
}
