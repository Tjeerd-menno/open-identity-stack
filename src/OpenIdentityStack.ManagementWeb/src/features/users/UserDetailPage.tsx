import { Badge, Button, Group, Stack, Tabs, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router';
import { Icon } from '@/components/Icon';
import { BackLink, CenteredState, DetailHeader, ErrorState, FieldRow, MetaStrip, SectionCard, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import { formatDateTime, formatRelativeTime } from '@/lib/format';

export function UserDetailPage() {
  const { userId = '' } = useParams();
  const auth = useAuth();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(auth.permissions, 'users:write');

  const userQuery = useQuery({ queryKey: ['user', userId], queryFn: () => api.users.getUser(userId) });
  const rolesQuery = useQuery({ queryKey: ['user', userId, 'roles'], queryFn: () => api.users.getUserRoles(userId) });
  const groupsQuery = useQuery({ queryKey: ['user', userId, 'groups'], queryFn: () => api.users.getUserGroups(userId) });
  const identitiesQuery = useQuery({
    queryKey: ['user', userId, 'identities'],
    queryFn: () => api.users.getUserUpstreamIdentities(userId),
  });

  const toggleStatus = useMutation({
    mutationFn: () =>
      userQuery.data?.status === 'Disabled'
        ? api.users.enableUser(userId)
        : api.users.disableUser(userId, { reason: 'Disabled from Management Web' }),
    onSuccess: () => {
      notifications.show({ message: 'User status updated', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['user', userId] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  if (userQuery.isLoading) {
    return <CenteredState loading title="Loading user…" />;
  }
  if (userQuery.isError || !userQuery.data) {
    return <ErrorState message={getApiErrorMessage(userQuery.error)} />;
  }

  const user = userQuery.data;
  const roles = rolesQuery.data ?? [];
  const groups = groupsQuery.data ?? [];
  const identities = identitiesQuery.data ?? [];

  return (
    <div>
      <BackLink label="Back to users" to="/users" />
      <DetailHeader
        avatarName={user.displayName}
        title={user.displayName}
        description={user.email}
        badge={<StatusBadge status={user.status} />}
        actions={
          canWrite ? (
            <Button
              variant="default"
              loading={toggleStatus.isPending}
              leftSection={<Icon name={user.status === 'Disabled' ? 'power' : 'ban'} size={16} />}
              onClick={() => toggleStatus.mutate()}
            >
              {user.status === 'Disabled' ? 'Enable user' : 'Disable user'}
            </Button>
          ) : undefined
        }
      />

      <MetaStrip
        items={[
          { label: 'Roles', value: roles.length },
          { label: 'Groups', value: groups.length },
          { label: 'MFA', value: user.mfaEnabled ? 'On' : 'Off' },
          { label: 'Created', value: formatDateTime(user.createdAt) },
        ]}
      />

      <Tabs defaultValue="profile" color="blue" keepMounted={false}>
        <Tabs.List mb="lg">
          <Tabs.Tab value="profile">Profile</Tabs.Tab>
          <Tabs.Tab value="roles">Roles ({roles.length})</Tabs.Tab>
          <Tabs.Tab value="groups">Groups ({groups.length})</Tabs.Tab>
          <Tabs.Tab value="identities">Upstream identities ({identities.length})</Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="profile">
          <SectionCard title="Profile">
            <FieldRow label="User ID" value={user.id} mono />
            <FieldRow label="Email" value={user.email} />
            <FieldRow label="MFA" value={user.mfaEnabled ? 'Enabled' : 'Not enabled'} />
            <FieldRow label="Last sign-in" value={formatRelativeTime(user.lastLoginAt)} />
            <FieldRow label="Created" value={formatDateTime(user.createdAt)} last />
          </SectionCard>
        </Tabs.Panel>

        <Tabs.Panel value="roles">
          <SectionCard title="Assigned roles" description="Platform roles assigned directly to this user.">
            {roles.length === 0 ? (
              <Text c="dimmed" size="sm">
                No roles assigned.
              </Text>
            ) : (
              <Group gap="xs">
                {roles.map((role) => (
                  <Badge key={role.id} color="blue" variant="light">
                    {role.displayName}
                  </Badge>
                ))}
              </Group>
            )}
          </SectionCard>
        </Tabs.Panel>

        <Tabs.Panel value="groups">
          <SectionCard title="Group membership">
            {groups.length === 0 ? (
              <Text c="dimmed" size="sm">
                Not a member of any group.
              </Text>
            ) : (
              <Stack gap={0}>
                {groups.map((group, index) => (
                  <FieldRow key={group.id} label={group.name} value={group.description ?? '—'} last={index === groups.length - 1} />
                ))}
              </Stack>
            )}
          </SectionCard>
        </Tabs.Panel>

        <Tabs.Panel value="identities">
          <SectionCard title="Upstream identities" description="Federated accounts linked to this user.">
            {identities.length === 0 ? (
              <Text c="dimmed" size="sm">
                No linked upstream identities.
              </Text>
            ) : (
              <Stack gap={0}>
                {identities.map((identity, index) => (
                  <FieldRow
                    key={`${identity.providerId}-${identity.subject}`}
                    label={identity.providerName ?? identity.providerId}
                    value={identity.subject}
                    mono
                    last={index === identities.length - 1}
                  />
                ))}
              </Stack>
            )}
          </SectionCard>
        </Tabs.Panel>
      </Tabs>
    </div>
  );
}
