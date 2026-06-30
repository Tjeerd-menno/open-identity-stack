import { Badge, Box, Button, Code, Group, Modal, Stack, Text, TextInput } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import type { AuditEntry } from '@openidentitystack/admin-api-client';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterToolbar } from '@/components/FilterToolbar';
import { Pager } from '@/components/ListControls';
import { Icon } from '@/components/Icon';
import { ErrorState, FieldRow, PageHeader, SectionCard } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { formatDateTime } from '@/lib/format';

const EMPTY_ADVANCED = { from: '', to: '', userId: '', action: '', entityId: '' };

export function AuditPage() {
  const [search, setSearch] = useState('');
  const [entity, setEntity] = useState('All');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  // Draft holds in-progress input; applied is what the query reads, so typing doesn't refetch
  // on every keystroke for the free-text/date filters.
  const [draft, setDraft] = useState(EMPTY_ADVANCED);
  const [applied, setApplied] = useState(EMPTY_ADVANCED);
  const [selected, setSelected] = useState<AuditEntry | null>(null);

  const query = useQuery({
    queryKey: ['audit-entries', { page, pageSize, search, entity, applied }],
    queryFn: () =>
      api.audit.getAuditEntries({
        page,
        pageSize,
        search: search || undefined,
        entityType: entity === 'All' ? undefined : entity,
        from: applied.from || undefined,
        // Append end-of-day time so that selecting 2026-06-30 includes all entries on that
        // date, not just those before midnight.
        to: applied.to ? `${applied.to}T23:59:59` : undefined,
        userId: applied.userId || undefined,
        action: applied.action || undefined,
        entityId: applied.entityId || undefined,
      }),
  });

  const applyAdvanced = () => {
    setApplied(draft);
    setPage(1);
  };
  const clearAdvanced = () => {
    setDraft(EMPTY_ADVANCED);
    setApplied(EMPTY_ADVANCED);
    setPage(1);
  };
  const hasAdvanced = Object.values(applied).some(Boolean);

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

      <Group align="flex-end" gap="sm" mb="md" wrap="wrap">
        <TextInput
          label="From"
          type="date"
          value={draft.from}
          w={150}
          onChange={(event) => setDraft((prev) => ({ ...prev, from: event.currentTarget.value }))}
        />
        <TextInput
          label="To"
          type="date"
          value={draft.to}
          w={150}
          onChange={(event) => setDraft((prev) => ({ ...prev, to: event.currentTarget.value }))}
        />
        <TextInput
          label="Action"
          placeholder="user.disabled"
          value={draft.action}
          w={170}
          onChange={(event) => setDraft((prev) => ({ ...prev, action: event.currentTarget.value }))}
        />
        <TextInput
          label="Actor (user ID)"
          placeholder="user id"
          value={draft.userId}
          w={170}
          onChange={(event) => setDraft((prev) => ({ ...prev, userId: event.currentTarget.value }))}
        />
        <TextInput
          label="Entity ID"
          placeholder="entity id"
          value={draft.entityId}
          w={170}
          onChange={(event) => setDraft((prev) => ({ ...prev, entityId: event.currentTarget.value }))}
        />
        <Button variant="default" onClick={applyAdvanced}>
          Apply
        </Button>
        {hasAdvanced && (
          <Button variant="subtle" color="gray" onClick={clearAdvanced}>
            Clear
          </Button>
        )}
      </Group>

      {query.isError ? (
        <ErrorState message={getApiErrorMessage(query.error)} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={query.data?.items ?? []}
            getRowKey={(entry) => entry.id}
            onRowClick={(entry) => setSelected(entry)}
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

      <AuditDetailModal entry={selected} onClose={() => setSelected(null)} />
    </div>
  );
}

function AuditDetailModal({ entry, onClose }: { entry: AuditEntry | null; onClose: () => void }) {
  return (
    <Modal opened={entry !== null} onClose={onClose} title="Audit entry" centered size="lg">
      {entry && (
        <Stack gap="lg">
          <SectionCard title="Summary">
            <FieldRow label="Action" value={entry.action} mono />
            <FieldRow label="Entity" value={`${entry.entityType} · ${entry.entityId}`} mono />
            <FieldRow label="Actor" value={entry.userId} mono />
            <FieldRow label="When" value={formatDateTime(entry.timestamp)} last />
          </SectionCard>

          {entry.details && (
            <Box>
              <Group gap={6} mb={6}>
                <Icon name="braces" size={15} color="var(--mw-text-muted)" />
                <Text fw={600} size="sm">Details</Text>
              </Group>
              <Code block>{entry.details}</Code>
            </Box>
          )}
          {entry.beforeState && (
            <Box>
              <Group gap={6} mb={6}>
                <Icon name="history" size={15} color="var(--mw-text-muted)" />
                <Text fw={600} size="sm">Before</Text>
              </Group>
              <Code block>{entry.beforeState}</Code>
            </Box>
          )}
          {entry.afterState && (
            <Box>
              <Group gap={6} mb={6}>
                <Icon name="git-compare" size={15} color="var(--mw-text-muted)" />
                <Text fw={600} size="sm">After</Text>
              </Group>
              <Code block>{entry.afterState}</Code>
            </Box>
          )}
          {!entry.details && !entry.beforeState && !entry.afterState && (
            <Text c="dimmed" size="sm">
              This entry has no recorded details or before/after state.
            </Text>
          )}
        </Stack>
      )}
    </Modal>
  );
}
