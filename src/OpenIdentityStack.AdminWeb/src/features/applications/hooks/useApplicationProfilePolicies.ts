import { useQuery } from '@tanstack/react-query';
import { getApplicationProfilePolicies } from '../api/applications-api';
import type { ApplicationProfilePolicy } from '@/types';

export const APPLICATION_PROFILE_POLICIES_QUERY_KEY = 'application-profile-policies';

export function useApplicationProfilePolicies() {
  return useQuery<ApplicationProfilePolicy[]>({
    queryKey: [APPLICATION_PROFILE_POLICIES_QUERY_KEY],
    queryFn: () => getApplicationProfilePolicies(),
    staleTime: 30000,
  });
}
