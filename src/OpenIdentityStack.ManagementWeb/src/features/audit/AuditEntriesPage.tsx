import { Alert, Code, Collapse, SimpleGrid, Stack, Text, TextInput, Title } from '@mantine/core';
import { useState } from 'react';
import { EntityActionGroup } from '@/components/EntityActionMenu';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
import { PageHeader, PageToolbar, type AppliedFilter } from '@/components/PagePrimitives';
import { getApiErrorMessage } from '@/lib/admin-api';
import { hasPermission } from '@/lib/permissions';
import { useAuditEntries } from './audit-entries-hooks';
import type { AuditEntry, AuditEntryListParams } from './audit-entries-api';

const pageSize = 20;

type AuditEntriesPageProps = {
  permissions?: string[];
};

type AuditFilterState = {
  from: string;
  to: string;
  userId: string;
  action: string;
  entityType: string;
  entityId: string;
  search: string;
};

const emptyFilters: AuditFilterState = {
  from: '',
  to: '',
  userId: '',
  action: '',
  entityType: '',
  entityId: '',
  search: '',
};

export function AuditEntriesPage({ permissions = ['*'] }: AuditEntriesPageProps) {
  const canReadAudit = hasPermission(permissions, 'audit-logs:read');
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<AuditFilterState>(emptyFilters);
  const [appliedFilters, setAppliedFilters] = useState<AuditFilterState>(emptyFilters);
  const [expandedEntryId, setExpandedEntryId] = useState<string | null>(null);
  const auditEntries = useAuditEntries(buildListParams(page, appliedFilters));

  if (!canReadAudit) {
    return (
      <Stack gap="sm">
        <Alert color="red">
          <Title order={1}>Access denied</Title>
          <Text>You do not have the required permission for this section.</Text>
          <Text size="sm">Required: audit-logs:read</Text>
        </Alert>
      </Stack>
    );
  }

  const columns: FoundationColumn<AuditEntry>[] = [
    {
      header: 'Timestamp',
      cell: (entry) => formatDate(entry.timestamp),
    },
    { header: 'User ID', accessorKey: 'userId' },
    { header: 'Action', accessorKey: 'action' },
    { header: 'Entity Type', accessorKey: 'entityType' },
    { header: 'Entity ID', accessorKey: 'entityId' },
    {
      header: 'Details',
      cell: (entry) => <Text size="sm" maw={260} truncate>{entry.details ?? 'No details'}</Text>,
    },
    {
      header: 'Actions',
      align: 'right',
      cell: (entry) => (
        <EntityActionGroup
          actions={[{
            label: expandedEntryId === entry.id ? `Collapse audit entry ${entry.id}` : `Expand audit entry ${entry.id}`,
            onClick: () => setExpandedEntryId(expandedEntryId === entry.id ? null : entry.id),
          }]}
        />
      ),
    },
  ];

  const expandedEntry = auditEntries.data?.items.find((entry) => entry.id === expandedEntryId) ?? null;
  const appliedFilterChips = toAppliedFilters(appliedFilters, (key) => {
    const next = { ...appliedFilters, [key]: '' };
    setFilters(next);
    setAppliedFilters(next);
    setPage(1);
    setExpandedEntryId(null);
  });

  return (
    <Stack gap="lg">
      <PageHeader
        title="Audit"
        description="Search and inspect administrative audit trail entries."
        badges={[{ label: 'Read only', color: 'blue' }]}
      />

      <PageToolbar
        searchLabel="Search audit entries"
        searchPlaceholder="Search details, action, actor, or entity..."
        searchValue={filters.search}
        onSearchChange={(value) => setFilters({ ...filters, search: value })}
        appliedFilters={appliedFilterChips}
        resultCount={auditEntries.data?.totalCount}
        onApply={() => {
          setAppliedFilters(filters);
          setPage(1);
          setExpandedEntryId(null);
        }}
        onClear={() => {
          setFilters(emptyFilters);
          setAppliedFilters(emptyFilters);
          setPage(1);
          setExpandedEntryId(null);
        }}
        secondaryFields={(
          <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }}>
            <TextInput
              label="User ID"
              value={filters.userId}
              onChange={(event) => setFilters({ ...filters, userId: event.currentTarget.value })}
            />
            <TextInput
              label="Action"
              value={filters.action}
              onChange={(event) => setFilters({ ...filters, action: event.currentTarget.value })}
            />
            <TextInput
              label="Entity type"
              value={filters.entityType}
              onChange={(event) => setFilters({ ...filters, entityType: event.currentTarget.value })}
            />
            <TextInput
              label="Entity ID"
              value={filters.entityId}
              onChange={(event) => setFilters({ ...filters, entityId: event.currentTarget.value })}
            />
            <TextInput
              label="From"
              type="datetime-local"
              value={filters.from}
              onChange={(event) => setFilters({ ...filters, from: event.currentTarget.value })}
            />
            <TextInput
              label="To"
              type="datetime-local"
              value={filters.to}
              onChange={(event) => setFilters({ ...filters, to: event.currentTarget.value })}
            />
          </SimpleGrid>
        )}
      />

      <FoundationTable
        columns={columns}
        data={auditEntries.data?.items ?? []}
        isLoading={auditEntries.isLoading}
        error={auditEntries.isError ? getApiErrorMessage(auditEntries.error) : null}
        emptyMessage="No audit entries found"
        pagination={{
          page,
          pageSize,
          totalCount: auditEntries.data?.totalCount ?? 0,
          totalPages: auditEntries.data?.totalPages ?? 0,
          onPageChange: (nextPage) => {
            setPage(nextPage);
            setExpandedEntryId(null);
          },
        }}
      />

      <Collapse in={expandedEntry !== null}>
        {expandedEntry && <AuditEntryDetails entry={expandedEntry} />}
      </Collapse>
    </Stack>
  );
}

function AuditEntryDetails({ entry }: { entry: AuditEntry }) {
  return (
    <Stack gap="sm" role="region" aria-label="Audit entry details">
      <Title order={2} size="h3">Audit Entry Details</Title>
      <Text size="sm">Details: {entry.details ?? 'No details'}</Text>
      <Stack gap={4}>
        <Text fw={600} size="sm">Before state</Text>
        <Code block>{entry.beforeState ?? 'null'}</Code>
      </Stack>
      <Stack gap={4}>
        <Text fw={600} size="sm">After state</Text>
        <Code block>{entry.afterState ?? 'null'}</Code>
      </Stack>
    </Stack>
  );
}

function buildListParams(page: number, filters: AuditFilterState): AuditEntryListParams {
  return {
    page,
    pageSize,
    from: toUtcIso(filters.from),
    to: toUtcIso(filters.to),
    userId: trimOrUndefined(filters.userId),
    action: trimOrUndefined(filters.action),
    entityType: trimOrUndefined(filters.entityType),
    entityId: trimOrUndefined(filters.entityId),
    search: trimOrUndefined(filters.search),
  };
}

function trimOrUndefined(value: string) {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

function toUtcIso(value: string) {
  if (!value) {
    return undefined;
  }

  return new Date(value).toISOString();
}

function formatDate(dateString: string) {
  return new Date(dateString).toLocaleString();
}

function toAppliedFilters(filters: AuditFilterState, onRemove: (key: keyof AuditFilterState) => void): AppliedFilter[] {
  return (Object.entries(filters) as Array<[keyof AuditFilterState, string]>)
    .filter(([, value]) => value.trim().length > 0)
    .map(([key, value]) => ({
      key,
      label: filterLabels[key],
      value,
      onRemove: () => onRemove(key),
    }));
}

const filterLabels: Record<keyof AuditFilterState, string> = {
  from: 'From',
  to: 'To',
  userId: 'User',
  action: 'Action',
  entityType: 'Entity type',
  entityId: 'Entity',
  search: 'Search',
};
