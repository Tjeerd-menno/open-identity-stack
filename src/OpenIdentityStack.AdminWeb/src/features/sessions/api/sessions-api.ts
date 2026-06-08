import { adminApiClient } from '@/lib/api/client';
import {
  createSessionsContract,
  type Session,
  type SessionListParams,
  type SessionListResponse,
  type RevokeAllUserSessionsResponse,
} from '@openidentitystack/admin-api-client';
import type { PaginatedResponse, SessionStatus } from '@/types';

const contract = createSessionsContract(adminApiClient);

export interface SessionsParams extends SessionListParams {
  status?: SessionStatus;
}

export type { Session, SessionListResponse, RevokeAllUserSessionsResponse, PaginatedResponse };
export type { SessionListParams };

export async function getSessions(
  params?: SessionsParams
): Promise<SessionListResponse> {
  return contract.getSessions(params as { page?: number; pageSize?: number; search?: string; status?: SessionStatus });
}

export async function getSession(sessionId: string): Promise<Session> {
  return contract.getSession(sessionId);
}

export async function revokeSession(
  sessionId: string
): Promise<void> {
  await contract.revokeSession(sessionId);
}

export async function revokeAllUserSessions(
  userId: string
): Promise<RevokeAllUserSessionsResponse> {
  return contract.revokeAllUserSessions(userId);
}
