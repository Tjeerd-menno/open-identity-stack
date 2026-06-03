import { useQuery } from '@tanstack/react-query';
import {
  getAuditEntries,
  type AuditEntryListParams,
  type AuditEntryListResponse,
} from './audit-entries-api';

export const AUDIT_ENTRIES_QUERY_KEY = 'audit-entries';

export function useAuditEntries(params?: AuditEntryListParams) {
  return useQuery<AuditEntryListResponse>({
    queryKey: [AUDIT_ENTRIES_QUERY_KEY, params],
    queryFn: () => getAuditEntries(params),
    staleTime: 30000,
    refetchOnMount: 'always',
  });
}
