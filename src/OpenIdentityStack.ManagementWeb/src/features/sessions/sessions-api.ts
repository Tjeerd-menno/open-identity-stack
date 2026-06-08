import { adminApiClient } from '@/lib/admin-api';
import {
  createSessionsContract,
  type RevokeAllUserSessionsResponse,
  type Session,
  type SessionListParams,
  type SessionListResponse,
  type SessionListResponse as SharedSessionListResponse,
  type PaginatedResponse,
} from '@openidentitystack/admin-api-client';

const contract = createSessionsContract(adminApiClient);

export type { Session, SessionListParams, RevokeAllUserSessionsResponse, PaginatedResponse };
export type SessionListResponse = SharedSessionListResponse;

export function getSessions(params?: SessionListParams): Promise<SessionListResponse> {
  return contract.getSessions(params);
}

export function getSession(sessionId: string): Promise<Session> {
  return contract.getSession(sessionId);
}

export function revokeSession(sessionId: string): Promise<void> {
  return contract.revokeSession(sessionId);
}

export function revokeAllUserSessions(userId: string): Promise<RevokeAllUserSessionsResponse> {
  return contract.revokeAllUserSessions(userId);
}
