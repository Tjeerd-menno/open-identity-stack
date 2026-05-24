import { useQuery } from '@tanstack/react-query';
import { listApplicationCredentials } from '../api/applications-api';
import { APPLICATIONS_QUERY_KEY } from './useApplications';
import type { ApplicationCredential } from '@/types';

export const applicationCredentialsQueryKey = (applicationId: string) =>
  [APPLICATIONS_QUERY_KEY, applicationId, 'credentials'] as const;

export function useApplicationCredentials(applicationId: string) {
  return useQuery<ApplicationCredential[]>({
    queryKey: applicationCredentialsQueryKey(applicationId),
    queryFn: () => listApplicationCredentials(applicationId),
    enabled: !!applicationId,
  });
}
