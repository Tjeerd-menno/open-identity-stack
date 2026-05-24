import { useQuery } from '@tanstack/react-query';
import { getApplications } from '../api/applications-api';
import type { ApplicationListParams, ApplicationListResponse } from '@/types';

export const APPLICATIONS_QUERY_KEY = 'applications';

export function useApplications(params?: ApplicationListParams) {
  return useQuery<ApplicationListResponse>({
    queryKey: [APPLICATIONS_QUERY_KEY, params],
    queryFn: () => getApplications(params),
    staleTime: 30000,
    refetchOnMount: 'always',
  });
}
