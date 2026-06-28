import { adminApiClient } from '@/lib/admin-api';
import {
  createAuditEntriesContract,
  type AuditEntry,
  type AuditEntryListParams,
  type AuditEntryListResponse,
} from '@openidentitystack/admin-api-client';

const contract = createAuditEntriesContract(adminApiClient);

export type { AuditEntry, AuditEntryListParams, AuditEntryListResponse };

export function getAuditEntries(params?: AuditEntryListParams): Promise<AuditEntryListResponse> {
  return contract.getAuditEntries(params);
}
