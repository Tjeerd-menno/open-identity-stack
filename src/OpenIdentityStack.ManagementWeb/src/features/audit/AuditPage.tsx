import { Badge, Box, Text } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import type { AuditEntry } from '@openidentitystack/admin-api-client';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterToolbar } from '@/components/FilterToolbar';
import { Pager } from '@/components/ListControls';
import { ErrorState, PageHeader } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { formatDateTime } from '@/lib/format';

export function AuditPage() {
  const [search, setSearch] = useState('');
  const [entity, setEntity] = useState('All');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  const query = useQuery({
    queryKey: ['audit-entries', { page, pageSize, search, entity }],
    queryFn: () =>
      api.audit.getAuditEntries({
        page,
        pageSize,
        search: search || undefined,
        entityType: entity === 'All' ? undefined : entity,
      }),
  });

  const columns: Column<AuditEntry>[] = [
    {
      key: 'action',
      header: 'Action',
      render: (entry) => (
        <Text className="mw-mono" fw={600} size="sm">
          {entry.action}
        </Text>
      ),
    },
    {
      key: 'entity',
      header: 'Entity',
      render: (entry) => (
        <Box style={{ minWidth: 0 }}>
          <Badge color="gray" variant="outline">
            {entry.entityType}
          </Badge>
          <Text c="dimmed" className="mw-mono" mt={4} size="xs" truncate>
            {entry.entityId}
          </Text>
        </Box>
      ),
    },
    {
      key: 'actor',
      header: 'Actor',
      render: (entry) => (
        <Text c="dimmed" className="mw-mono" size="sm">
          {entry.userId}
        </Text>
      ),
    },
    {
      key: 'when',
      header: 'When',
      align: 'right',
      render: (entry) => (
        <Text c="dimmed" size="sm">
          {formatDateTime(entry.timestamp)}
        </Text>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title="Audit" description="A read-only trail of administrative actions across the tenant." />

      <FilterToolbar
        noun="entries"
        search={search}
        onSearch={(value) => { setSearch(value); setPage(1); }}
        filters={[
          {
            key: 'entity',
            label: 'Entity',
            value: entity,
            options: ['All', 'User', 'Application', 'Role', 'Group', 'Session', 'Provider'],
            onChange: (value) => { setEntity(value); setPage(1); },
          },
        ]}
        rows={pageSize}
        onRows={(size) => { setPageSize(size); setPage(1); }}
        count={query.data?.totalCount}
      />

      {query.isError ? (
        <ErrorState message={getApiErrorMessage(query.error)} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={query.data?.items ?? []}
            getRowKey={(entry) => entry.id}
            isLoading={query.isLoading}
            emptyIcon="scroll-text"
            emptyTitle="No audit entries"
            emptyText="There are no audit entries matching your search."
            minWidth={760}
          />
          <Pager
            page={page}
            pageSize={pageSize}
            totalCount={query.data?.totalCount ?? 0}
            totalPages={query.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
          />
        </>
      )}
    </div>
  );
}
