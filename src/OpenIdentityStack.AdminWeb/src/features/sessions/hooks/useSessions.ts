import { useQuery } from '@tanstack/react-query';
import { getSessions, type SessionsParams } from '../api/sessions-api';

export interface UseSessionsOptions extends SessionsParams {
  /** Whether the query should run. Defaults to true. */
  enabled?: boolean;
}

export function useSessions(options: UseSessionsOptions = {}) {
  const { enabled = true, ...params } = options;
  return useQuery({
    queryKey: ['sessions', params],
    queryFn: () => getSessions(params),
    staleTime: 30000, // 30 seconds
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
    enabled,
  });
}
