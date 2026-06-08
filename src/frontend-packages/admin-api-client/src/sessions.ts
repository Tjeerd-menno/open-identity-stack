import type { AdminApiClient, PaginatedResponse } from './index';

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

export type SessionsContract = {
  getSessions: (params?: SessionListParams) => Promise<SessionListResponse>;
  getSession: (sessionId: string) => Promise<Session>;
  revokeSession: (sessionId: string) => Promise<void>;
  revokeAllUserSessions: (userId: string) => Promise<RevokeAllUserSessionsResponse>;
};

export function createSessionsContract(client: AdminApiClient): SessionsContract {
  return {
    getSessions: (params) => client.get<SessionListResponse>('/api/admin/sessions', params),
    getSession: (sessionId) => client.get<Session>(`/api/admin/sessions/${sessionId}`),
    revokeSession: (sessionId) => client.delete<void>(`/api/admin/sessions/${sessionId}`),
    revokeAllUserSessions: (userId) =>
      client.delete<RevokeAllUserSessionsResponse>(`/api/admin/users/${userId}/sessions`),
  };
}
