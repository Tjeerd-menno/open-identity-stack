import type { AdminApiClient } from './index';

export type CutoverBlocker = { code: string; message: string; count: number };
export type EmergencyAccessEvidence = { id: string; userId: string; sessionId: string; authenticatedAt: string; recordedAt: string; currentlyUsable: boolean };
export type ResourceTokenWindow = { resourceId: string; displayName: string; audience: string; scope: string; revision: number; mechanism: string | null; residualSeconds: number | null; evidenceReference: string | null; reviewedAt: string | null; reviewed: boolean };
export type CutoverPreflight = {
  epoch: string; evaluatedAt: string; ready: boolean; blockers: CutoverBlocker[]; emergencyAccess: EmergencyAccessEvidence | null;
  identities: { quarantinedLinks: number; affectedUsers: number; federationOnlyUsers: number; passwordCandidates: number; disabledUsers: number; verifiedEmails: number; providerEvidence: number; withdrawnEvidence: number };
  administrativeClients: { id: string; clientId: string; active: boolean; approved: boolean; delegatedPermissions: string[]; applicationPermissions: string[]; requiresMigrationReview: boolean }[];
  businessResources: ResourceTokenWindow[]; outstandingAccessTokens: number; latestAccessTokenExpiry: string | null;
};
export type ResourceWindowReview = { mechanism: string; residualSeconds: number; evidenceReference: string };
export type CredentialCutoverResult = { operationId: string; completedAt: string; tokens: number; grants: number; sessions: number };
export function createCutoverContract(client: AdminApiClient) {
  return {
    getReadiness: () => client.get<CutoverPreflight>('/api/admin/security/cutover-readiness'),
    recordEmergencyAccess: () => client.post<EmergencyAccessEvidence>('/api/admin/security/emergency-access-evidence', {}),
    reviewResourceWindow: (resourceId: string, review: ResourceWindowReview) => client.put<void>(`/api/admin/security/business-resources/${resourceId}/token-window-review`, review),
    execute: (operationId: string) => client.post<CredentialCutoverResult>('/api/admin/security/credential-cutovers', { operationId }),
  };
}
