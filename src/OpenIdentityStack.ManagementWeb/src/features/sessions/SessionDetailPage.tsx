import { Button, Group, Stack } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useParams } from 'react-router';
import { Icon } from '@/components/Icon';
import { ConfirmModal } from '@/components/ConfirmModal';
import { BackLink, CenteredState, DetailHeader, ErrorState, FieldRow, MetaStrip, SectionCard, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import { formatDateTime, formatRelativeTime } from '@/lib/format';

/**
 * Detail view for a single session. Beyond inspecting metadata, it exposes both per-session
 * revocation and revoke-all-for-this-user — the workflow the AdminWeb session detail provided.
 */
export function SessionDetailPage() {
  const { sessionId = '' } = useParams();
  const auth = useAuth();
  const queryClient = useQueryClient();
  const canRevoke = hasPermission(auth.permissions, 'sessions:revoke');
  const [pendingRevoke, setPendingRevoke] = useState(false);
  const [pendingRevokeAll, setPendingRevokeAll] = useState(false);

  const sessionQuery = useQuery({ queryKey: ['session', sessionId], queryFn: () => api.sessions.getSession(sessionId) });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['session', sessionId] });
    void queryClient.invalidateQueries({ queryKey: ['sessions'] });
  };

  const revoke = useMutation({
    mutationFn: () => api.sessions.revokeSession(sessionId),
    onSuccess: () => {
      notifications.show({ message: 'Session revoked', color: 'green' });
      setPendingRevoke(false);
      invalidate();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const revokeAll = useMutation({
    mutationFn: (userId: string) => api.sessions.revokeAllUserSessions(userId),
    onSuccess: (result) => {
      notifications.show({ message: `Revoked ${result.revokedCount} session(s) for this user`, color: 'green' });
      setPendingRevokeAll(false);
      invalidate();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  if (sessionQuery.isLoading) {
    return <CenteredState loading title="Loading session…" />;
  }
  if (sessionQuery.isError || !sessionQuery.data) {
    return <ErrorState message={getApiErrorMessage(sessionQuery.error)} />;
  }

  const session = sessionQuery.data;

  return (
    <div>
      <BackLink label="Back to sessions" to="/sessions" />
      <DetailHeader
        icon="activity"
        title={session.id}
        description={`User ${session.userId}`}
        badge={<StatusBadge status={session.status} />}
        actions={
          canRevoke ? (
            <>
              <Button
                variant="default"
                leftSection={<Icon name="users-round" size={16} />}
                onClick={() => setPendingRevokeAll(true)}
              >
                Revoke all user sessions
              </Button>
              <Button
                variant="default"
                leftSection={<Icon name="ban" size={16} />}
                disabled={session.status !== 'Active'}
                onClick={() => setPendingRevoke(true)}
              >
                Revoke session
              </Button>
            </>
          ) : undefined
        }
      />

      <MetaStrip
        items={[
          { label: 'Status', value: session.status },
          { label: 'Clients', value: session.clientCount },
          { label: 'Last active', value: formatRelativeTime(session.lastActivityAt) },
          { label: 'Expires', value: formatRelativeTime(session.expiresAt) },
        ]}
      />

      <Stack gap="lg">
        <SectionCard title="Session">
          <FieldRow label="Session ID" value={session.id} mono />
          <FieldRow label="User ID" value={session.userId} mono />
          <FieldRow label="IP address" value={session.ipAddress} mono />
          <FieldRow label="User agent" value={session.userAgent} />
          <FieldRow label="Created" value={formatDateTime(session.createdAt)} />
          <FieldRow label="Expires" value={formatDateTime(session.expiresAt)} last />
        </SectionCard>
      </Stack>

      <Group mt="lg" />

      <ConfirmModal
        opened={pendingRevoke}
        title="Revoke session"
        message="This signs the session out immediately and forces re-authentication. This cannot be undone."
        confirmLabel="Revoke"
        loading={revoke.isPending}
        onConfirm={() => revoke.mutate()}
        onClose={() => setPendingRevoke(false)}
      />
      <ConfirmModal
        opened={pendingRevokeAll}
        title="Revoke all sessions for this user"
        message="Every active session for this user is signed out immediately, forcing re-authentication on all of their devices."
        confirmLabel="Revoke all"
        loading={revokeAll.isPending}
        onConfirm={() => revokeAll.mutate(session.userId)}
        onClose={() => setPendingRevokeAll(false)}
      />
    </div>
  );
}
