import { Alert, Badge, Button, Group, Stack, Text, Title } from '@mantine/core';
import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { EntityActionGroup } from '@/components/EntityActionMenu';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
import { PageHeader, PageToolbar } from '@/components/PagePrimitives';
import { getApiErrorMessage } from '@/lib/admin-api';
import { hasPermission } from '@/lib/permissions';
import {
  useRevokeAllUserSessions,
  useRevokeSession,
  useSession,
  useSessions,
} from './sessions-hooks';
import type { Session, SessionStatus } from './sessions-api';

const pageSize = 20;

type SessionsPageProps = {
  permissions?: string[];
};

export function SessionsPage({ permissions = ['*'] }: SessionsPageProps) {
  const location = useLocation();
  const { id } = useParams();
  const pathSessionId = getSessionIdFromPath(location.pathname);

  if (id || pathSessionId) {
    return <SessionDetailView sessionId={id ?? pathSessionId ?? ''} permissions={permissions} />;
  }

  return <SessionListView permissions={permissions} />;
}

function getSessionIdFromPath(pathname: string) {
  return /^\/sessions\/([^/]+)$/.exec(pathname)?.[1];
}

function SessionListView({ permissions }: Required<SessionsPageProps>) {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [status, setStatus] = useState<SessionStatus | 'All'>('Active');
  const [sessionToRevoke, setSessionToRevoke] = useState<Session | null>(null);
  const searchMounted = useRef(false);
  const sessions = useSessions({
    page,
    pageSize,
    search: submittedSearch || undefined,
    status: status === 'All' ? undefined : status,
  });
  const revokeSessionMutation = useRevokeSession(sessionToRevoke?.id ?? '');
  const canRevoke = hasPermission(permissions, 'sessions:revoke');

  useEffect(() => {
    if (!searchMounted.current) {
      searchMounted.current = true;
      return;
    }

    const timer = window.setTimeout(() => {
      setSubmittedSearch(search);
      setPage(1);
    }, 300);

    return () => window.clearTimeout(timer);
  }, [search]);

  const columns: FoundationColumn<Session>[] = [
    {
      header: 'User ID',
      cell: (session) => <Text ff="monospace" size="sm">{shortId(session.userId)}</Text>,
    },
    {
      header: 'IP Address',
      cell: (session) => <Text ff="monospace" size="sm" c="dimmed">{session.ipAddress}</Text>,
    },
    {
      header: 'User Agent',
      cell: (session) => <Text size="sm" maw={260} truncate>{session.userAgent}</Text>,
    },
    {
      header: 'Status',
      cell: (session) => <SessionStatusBadge status={session.status} />,
    },
    { header: 'Applications', accessorKey: 'clientCount' },
    {
      header: 'Last Activity',
      cell: (session) => formatDate(session.lastActivityAt),
    },
    {
      header: 'Expires',
      cell: (session) => formatDate(session.expiresAt),
    },
    {
      header: 'Actions',
      align: 'right',
      cell: (session) => (
        <EntityActionGroup
          actions={[
            { label: `View session ${session.id}`, onClick: () => navigate(`/sessions/${session.id}`) },
            ...(canRevoke && session.status === 'Active'
              ? [{ label: `Revoke session ${session.id}`, color: 'red' as const, onClick: () => setSessionToRevoke(session) }]
              : []),
          ]}
        />
      ),
    },
  ];

  return (
    <Stack gap="lg">
      <PageHeader
        title="Sessions"
        description="View and manage active user sessions."
        badges={[{ label: status === 'All' ? 'All statuses' : status, color: status === 'Active' ? 'green' : 'gray' }]}
      />

      {!canRevoke && <Alert color="blue">Read-only access. Session revocation requires sessions:revoke.</Alert>}

      <PageToolbar
        searchLabel="Search sessions"
        searchPlaceholder="Search by user ID or IP address..."
        searchValue={search}
        resultCount={sessions.data?.totalCount}
        onSearchChange={setSearch}
        filters={[{
          key: 'status',
          label: 'Filter by status',
          value: status,
          onChange: (value) => {
            setStatus((value ?? 'All') as SessionStatus | 'All');
            setPage(1);
          },
          data: [
            { value: 'All', label: 'All Sessions' },
            { value: 'Active', label: 'Active' },
            { value: 'LoggedOut', label: 'Logged Out' },
            { value: 'Expired', label: 'Expired' },
            { value: 'Revoked', label: 'Revoked' },
          ],
        }]}
        appliedFilters={status === 'All' ? [] : [{ key: 'status', label: 'Status', value: status, onRemove: () => setStatus('All') }]}
        onClear={() => {
          setSearch('');
          setSubmittedSearch('');
          setStatus('All');
          setPage(1);
        }}
      />

      <FoundationTable
        columns={columns}
        data={sessions.data?.items ?? []}
        isLoading={sessions.isLoading}
        error={sessions.isError ? getApiErrorMessage(sessions.error) : null}
        emptyMessage="No sessions found"
        pagination={{
          page,
          pageSize,
          totalCount: sessions.data?.totalCount ?? 0,
          totalPages: sessions.data?.totalPages ?? 0,
          onPageChange: setPage,
        }}
      />

      <ConfirmDialog
        opened={sessionToRevoke !== null}
        onOpenChange={(opened) => !opened && setSessionToRevoke(null)}
        title="Revoke session"
        message="Are you sure you want to revoke this session? The user will be immediately logged out."
        confirmLabel="Revoke session"
        loading={revokeSessionMutation.isPending}
        onConfirm={async () => {
          await revokeSessionMutation.mutateAsync();
          setSessionToRevoke(null);
        }}
      />
    </Stack>
  );
}

function SessionDetailView({ sessionId, permissions }: { sessionId: string; permissions: string[] }) {
  const navigate = useNavigate();
  const session = useSession(sessionId);
  const revokeSessionMutation = useRevokeSession(sessionId);
  const revokeAllMutation = useRevokeAllUserSessions(session.data?.userId ?? '');
  const [confirmRevoke, setConfirmRevoke] = useState(false);
  const [confirmRevokeAll, setConfirmRevokeAll] = useState(false);
  const canRevoke = hasPermission(permissions, 'sessions:revoke');

  if (session.isLoading) {
    return <Text>Loading session</Text>;
  }

  if (session.isError) {
    return <Alert color="red">{getApiErrorMessage(session.error)}</Alert>;
  }

  if (!session.data) {
    return <Alert color="red">Session not found.</Alert>;
  }

  const isActive = session.data.status === 'Active';

  return (
    <Stack gap="lg" role="region" aria-label="Session details">
      <Group justify="space-between" align="flex-start">
        <div>
          <Title order={1}>Session Details</Title>
          <Text c="dimmed" ff="monospace">{session.data.id}</Text>
        </div>
        <Group>
          <Button variant="default" onClick={() => navigate('/sessions')}>Back to Sessions</Button>
          {canRevoke && isActive && (
            <>
              <Button color="red" variant="light" onClick={() => setConfirmRevoke(true)}>Revoke session</Button>
              <Button color="red" onClick={() => setConfirmRevokeAll(true)}>Revoke all sessions for user</Button>
            </>
          )}
        </Group>
      </Group>

      {!canRevoke && <Alert color="blue">Read-only access. Session revocation requires sessions:revoke.</Alert>}

      <section aria-label="Session information">
        <Title order={2} size="h3">Session Information</Title>
        <Stack gap={4} mt="sm">
          <Text size="sm">Session ID: {session.data.id}</Text>
          <Group gap="xs">
            <Text size="sm">Status:</Text>
            <SessionStatusBadge status={session.data.status} />
          </Group>
          <Text size="sm">User ID: {session.data.userId}</Text>
          <Text size="sm">Application count: {session.data.clientCount}</Text>
          <Text size="sm">IP address: {session.data.ipAddress}</Text>
          <Text size="sm">User agent: {session.data.userAgent}</Text>
          <Text size="sm">Created: {formatDate(session.data.createdAt)}</Text>
          <Text size="sm">Last activity: {formatDate(session.data.lastActivityAt)}</Text>
          <Text size="sm">Expires: {formatDate(session.data.expiresAt)}</Text>
        </Stack>
      </section>

      <ConfirmDialog
        opened={confirmRevoke}
        onOpenChange={setConfirmRevoke}
        title="Revoke session"
        message="Are you sure you want to revoke this session? The user will be immediately logged out from all applications associated with this session."
        confirmLabel="Revoke session"
        loading={revokeSessionMutation.isPending}
        onConfirm={async () => {
          await revokeSessionMutation.mutateAsync();
          navigate('/sessions');
        }}
      />

      <ConfirmDialog
        opened={confirmRevokeAll}
        onOpenChange={setConfirmRevokeAll}
        title="Revoke all user sessions"
        message="Revoke all sessions for this user? They will be signed out from every active session."
        confirmLabel="Revoke all user sessions"
        loading={revokeAllMutation.isPending}
        onConfirm={async () => {
          await revokeAllMutation.mutateAsync();
          setConfirmRevokeAll(false);
        }}
      />
    </Stack>
  );
}

function SessionStatusBadge({ status }: { status: SessionStatus }) {
  const colors: Record<SessionStatus, string> = {
    Active: 'green',
    LoggedOut: 'gray',
    Expired: 'yellow',
    Revoked: 'red',
  };

  return <Badge color={colors[status]} variant="light">{status}</Badge>;
}

function formatDate(dateString: string) {
  return new Date(dateString).toLocaleString();
}

function shortId(value: string) {
  return value.length > 8 ? `${value.substring(0, 8)}...` : value;
}
