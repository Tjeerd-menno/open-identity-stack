import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getSession,
  getSessions,
  revokeAllUserSessions,
  revokeSession,
  type RevokeAllUserSessionsResponse,
  type Session,
  type SessionListParams,
  type SessionListResponse,
} from './sessions-api';

export const SESSIONS_QUERY_KEY = 'sessions';

export function useSessions(params?: SessionListParams) {
  return useQuery<SessionListResponse>({
    queryKey: [SESSIONS_QUERY_KEY, params],
    queryFn: () => getSessions(params),
    staleTime: 30000,
    refetchOnMount: 'always',
  });
}

export function useSession(sessionId: string | undefined) {
  return useQuery<Session>({
    queryKey: [SESSIONS_QUERY_KEY, sessionId],
    queryFn: () => getSession(sessionId ?? ''),
    enabled: !!sessionId,
  });
}

export function useRevokeSession(sessionId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => revokeSession(sessionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [SESSIONS_QUERY_KEY] });
      queryClient.invalidateQueries({ queryKey: [SESSIONS_QUERY_KEY, sessionId] });
    },
  });
}

export function useRevokeAllUserSessions(userId: string) {
  const queryClient = useQueryClient();

  return useMutation<RevokeAllUserSessionsResponse>({
    mutationFn: () => revokeAllUserSessions(userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [SESSIONS_QUERY_KEY] }),
  });
}
