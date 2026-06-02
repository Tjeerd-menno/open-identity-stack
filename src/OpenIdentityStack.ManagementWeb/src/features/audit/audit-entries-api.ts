import { request, type PaginatedResponse } from '@/lib/admin-api';

export type AuditEntry = {
  id: string;
  timestamp: string;
  userId: string;
  action: string;
  entityType: string;
  entityId: string;
  details: string | null;
  beforeState: string | null;
  afterState: string | null;
};

export type AuditEntryListParams = {
  page?: number;
  pageSize?: number;
  from?: string;
  to?: string;
  userId?: string;
  action?: string;
  entityType?: string;
  entityId?: string;
  search?: string;
};

export type AuditEntryListResponse = PaginatedResponse<AuditEntry> & {
  hasPreviousPage?: boolean;
  hasNextPage?: boolean;
};

export function getAuditEntries(params?: AuditEntryListParams): Promise<AuditEntryListResponse> {
  return request<AuditEntryListResponse>('/api/admin/audit-entries', {}, params);
}
