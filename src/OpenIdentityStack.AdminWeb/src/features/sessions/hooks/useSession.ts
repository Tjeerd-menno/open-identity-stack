import { useQuery } from '@tanstack/react-query';
import { getSession } from '../api/sessions-api';

export function useSession(sessionId: string) {
  return useQuery({
    queryKey: ['sessions', sessionId],
    queryFn: () => getSession(sessionId),
    staleTime: 30000, // 30 seconds
    refetchOnMount: 'always', // Always refetch when component mounts for E2E reliability
  });
}
