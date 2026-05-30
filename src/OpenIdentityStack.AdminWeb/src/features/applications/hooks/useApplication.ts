import { useQuery } from '@tanstack/react-query';
import { getApplication } from '../api/applications-api';
import { APPLICATIONS_QUERY_KEY } from './useApplications';
import type { Application } from '@/types';

export function useApplication(applicationId: string) {
  return useQuery<Application>({
    queryKey: [APPLICATIONS_QUERY_KEY, applicationId],
    queryFn: () => getApplication(applicationId),
    enabled: !!applicationId,
  });
}
