import { Alert, Button, Code, Collapse, Group, SimpleGrid, Stack, Text, TextInput, Title } from '@mantine/core';
import { useState } from 'react';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
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
      cell: (entry) => (
        <Button
          variant="subtle"
          size="xs"
          aria-label="Expand audit entry"
          onClick={() => setExpandedEntryId(expandedEntryId === entry.id ? null : entry.id)}
        >
          {expandedEntryId === entry.id ? 'Collapse' : 'Expand'}
        </Button>
      ),
    },
  ];

  const expandedEntry = auditEntries.data?.items.find((entry) => entry.id === expandedEntryId) ?? null;

  return (
    <Stack gap="lg">
      <div>
        <Title order={1}>Audit</Title>
        <Text c="dimmed">View administrative audit trail entries.</Text>
      </div>

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
        <TextInput
          label="Search audit entries"
          value={filters.search}
          onChange={(event) => setFilters({ ...filters, search: event.currentTarget.value })}
        />
      </SimpleGrid>

      <Group>
        <Button
          onClick={() => {
            setAppliedFilters(filters);
            setPage(1);
            setExpandedEntryId(null);
          }}
        >
          Apply filters
        </Button>
        <Button
          variant="default"
          onClick={() => {
            setFilters(emptyFilters);
            setAppliedFilters(emptyFilters);
            setPage(1);
            setExpandedEntryId(null);
          }}
        >
          Clear filters
        </Button>
      </Group>

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
