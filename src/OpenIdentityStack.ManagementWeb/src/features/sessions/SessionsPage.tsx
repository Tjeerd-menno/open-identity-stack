import { Box, Group, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import type { Session, SessionStatus } from '@openidentitystack/admin-api-client';
import { Icon, type IconName } from '@/components/Icon';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterToolbar } from '@/components/FilterToolbar';
import { Pager } from '@/components/ListControls';
import { RowMenu } from '@/components/RowMenu';
import { ConfirmModal } from '@/components/ConfirmModal';
import { ErrorState, PageHeader, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import { formatRelativeTime } from '@/lib/format';

function deriveDevice(userAgent: string): { label: string; icon: IconName } {
  if (/iPhone|Android|Mobile/i.test(userAgent)) return { label: 'Mobile', icon: 'smartphone' };
  if (/client_credentials|Worker|Service/i.test(userAgent)) return { label: 'Service', icon: 'server' };
  if (/Mozilla|Chrome|Safari|Firefox|Edg/i.test(userAgent)) return { label: 'Browser', icon: 'monitor' };
  return { label: 'Client', icon: 'globe' };
}

export function SessionsPage() {
  const auth = useAuth();
  const queryClient = useQueryClient();
  const canRevoke = hasPermission(auth.permissions, 'sessions:revoke');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('All');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [pendingRevoke, setPendingRevoke] = useState<Session | null>(null);

  const query = useQuery({
    queryKey: ['sessions', { page, pageSize, search, status }],
    queryFn: () =>
      api.sessions.getSessions({
        page,
        pageSize,
        search: search || undefined,
        status: status === 'All' ? undefined : (status as SessionStatus),
      }),
  });

  const revoke = useMutation({
    mutationFn: (sessionId: string) => api.sessions.revokeSession(sessionId),
    onSuccess: () => {
      notifications.show({ message: 'Session revoked', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['sessions'] });
      setPendingRevoke(null);
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const copyId = (id: string) => {
    void navigator.clipboard?.writeText(id);
    notifications.show({ message: 'Session ID copied', color: 'gray' });
  };

  const columns: Column<Session>[] = [
    {
      key: 'device',
      header: 'Device',
      render: (session) => {
        const device = deriveDevice(session.userAgent);
        return (
          <Group gap="sm" wrap="nowrap">
            <Icon name={device.icon} size={18} color="var(--mw-text-muted)" />
            <Box style={{ minWidth: 0 }}>
              <Text fw={600} size="sm">{device.label}</Text>
              <Text c="dimmed" className="mw-mono" size="xs" truncate>{session.ipAddress}</Text>
            </Box>
          </Group>
        );
      },
    },
    { key: 'status', header: 'Status', render: (session) => <StatusBadge status={session.status} /> },
    { key: 'last', header: 'Last active', render: (session) => <Text c="dimmed" size="sm">{formatRelativeTime(session.lastActivityAt)}</Text> },
    { key: 'expires', header: 'Expires', render: (session) => <Text c="dimmed" size="sm">{formatRelativeTime(session.expiresAt)}</Text> },
    {
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (session) => (
        <RowMenu
          items={[
            { label: 'Copy session ID', icon: 'copy', onClick: () => copyId(session.id) },
            { separator: true },
            {
              label: 'Revoke session',
              icon: 'ban',
              danger: true,
              disabled: !canRevoke || session.status !== 'Active',
              onClick: () => setPendingRevoke(session),
            },
          ]}
        />
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Sessions"
        description="Active, expired and revoked user sessions. Revoke to force re-authentication."
      />

      <FilterToolbar
        noun="sessions"
        search={search}
        onSearch={(value) => { setSearch(value); setPage(1); }}
        filters={[
          {
            key: 'status',
            label: 'Status',
            value: status,
            options: ['All', 'Active', 'LoggedOut', 'Expired', 'Revoked'],
            onChange: (value) => { setStatus(value); setPage(1); },
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
            getRowKey={(session) => session.id}
            isLoading={query.isLoading}
            emptyIcon="activity"
            emptyTitle="No sessions"
            emptyText="There are no sessions matching your filters."
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

      <ConfirmModal
        opened={pendingRevoke !== null}
        title="Revoke session"
        message="This signs the session out immediately and forces re-authentication. This cannot be undone."
        confirmLabel="Revoke"
        loading={revoke.isPending}
        onConfirm={() => pendingRevoke && revoke.mutate(pendingRevoke.id)}
        onClose={() => setPendingRevoke(null)}
      />
    </div>
  );
}
