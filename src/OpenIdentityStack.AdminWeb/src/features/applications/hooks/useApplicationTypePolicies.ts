import { useQuery } from '@tanstack/react-query';
import { getApplicationTypePolicies } from '../api/applications-api';
import type { ApplicationTypePolicy } from '@/types';

export const APPLICATION_TYPE_POLICIES_QUERY_KEY = 'application-type-policies';

export function useApplicationTypePolicies() {
  return useQuery<ApplicationTypePolicy[]>({
    queryKey: [APPLICATION_TYPE_POLICIES_QUERY_KEY],
    queryFn: () => getApplicationTypePolicies(),
    staleTime: 30000,
  });
}
