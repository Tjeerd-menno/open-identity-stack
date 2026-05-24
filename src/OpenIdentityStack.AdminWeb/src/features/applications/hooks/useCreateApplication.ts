import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createApplication } from '../api/applications-api';
import { APPLICATIONS_QUERY_KEY } from './useApplications';
import type { ApplicationCreatedResponse, CreateApplicationRequest } from '@/types';

export function useCreateApplication() {
  const queryClient = useQueryClient();

  return useMutation<ApplicationCreatedResponse, Error, CreateApplicationRequest>({
    mutationFn: createApplication,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY] });
    },
  });
}
