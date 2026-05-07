/**
 * Sessions API Client
 * 
 * Provides functions for interacting with the session management endpoints
 */

import { apiClient } from '@/lib/api/client';
import type {
  Session,
  SessionListResponse,
  RevokeSessionResponse,
  RevokeAllSessionsResponse,
  PaginationParams,
  SessionStatus,
} from '@/types';

export interface SessionsParams extends PaginationParams {
  status?: SessionStatus;
}

/**
 * Get paginated list of sessions
 */
export async function getSessions(
  params?: SessionsParams
): Promise<SessionListResponse> {
  return await apiClient.get<SessionListResponse>(
    '/api/admin/sessions',
    params
  );
}

/**
 * Get a single session by ID
 */
export async function getSession(
  sessionId: string
): Promise<Session> {
  return await apiClient.get<Session>(
    `/api/admin/sessions/${sessionId}`
  );
}

/**
 * Revoke a single session
 */
export async function revokeSession(
  sessionId: string
): Promise<RevokeSessionResponse> {
  return await apiClient.delete<RevokeSessionResponse>(
    `/api/admin/sessions/${sessionId}`
  );
}

/**
 * Revoke all sessions for a specific user
 */
export async function revokeAllUserSessions(
  userId: string
): Promise<RevokeAllSessionsResponse> {
  return await apiClient.post<RevokeAllSessionsResponse>(
    `/api/admin/users/${userId}/sessions/revoke-all`
  );
}
