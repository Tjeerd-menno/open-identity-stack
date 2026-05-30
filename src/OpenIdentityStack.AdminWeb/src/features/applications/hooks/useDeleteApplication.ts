import { useMutation, useQueryClient } from '@tanstack/react-query';
import { deleteApplication } from '../api/applications-api';
import { APPLICATIONS_QUERY_KEY } from './useApplications';

export function useDeleteApplication() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, string>({
    mutationFn: deleteApplication,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY] });
    },
  });
}
