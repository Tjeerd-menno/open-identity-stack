import { request, type PaginatedResponse } from '@/lib/admin-api';

export type SessionStatus = 'Active' | 'LoggedOut' | 'Expired' | 'Revoked';

export type Session = {
  id: string;
  userId: string;
  ipAddress: string;
  userAgent: string;
  status: SessionStatus;
  clientCount: number;
  lastActivityAt: string;
  expiresAt: string;
  createdAt: string;
};

export type SessionListParams = {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: SessionStatus;
};

export type SessionListResponse = PaginatedResponse<Session>;

export type RevokeAllUserSessionsResponse = {
  revokedCount: number;
  revokedAt: string;
};

export function getSessions(params?: SessionListParams): Promise<SessionListResponse> {
  return request<SessionListResponse>('/api/admin/sessions', {}, params);
}

export function getSession(sessionId: string): Promise<Session> {
  return request<Session>(`/api/admin/sessions/${sessionId}`);
}

export function revokeSession(sessionId: string): Promise<void> {
  return request<void>(`/api/admin/sessions/${sessionId}`, {
    method: 'DELETE',
  });
}

export function revokeAllUserSessions(userId: string): Promise<RevokeAllUserSessionsResponse> {
  return request<RevokeAllUserSessionsResponse>(`/api/admin/users/${userId}/sessions`, {
    method: 'DELETE',
  });
}
