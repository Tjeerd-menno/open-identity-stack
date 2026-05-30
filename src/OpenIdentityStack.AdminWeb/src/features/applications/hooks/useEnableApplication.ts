import { useMutation, useQueryClient } from '@tanstack/react-query';
import { enableApplication } from '../api/applications-api';
import { APPLICATIONS_QUERY_KEY } from './useApplications';
import type { Application } from '@/types';

export function useEnableApplication() {
  const queryClient = useQueryClient();

  return useMutation<Application, Error, string>({
    mutationFn: enableApplication,
    onSuccess: async (data, applicationId) => {
      queryClient.setQueryData([APPLICATIONS_QUERY_KEY, applicationId], data);
      await queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY] });
    },
  });
}
