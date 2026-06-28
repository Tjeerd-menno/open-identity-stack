import { ActionIcon, Badge, Box, Button, Group, Select, Stack, Tabs, Text, TextInput } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
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
  const canAssignRoles = canWrite && hasPermission(auth.permissions, 'roles:read');
  const [roleToAssign, setRoleToAssign] = useState<string | null>(null);

  const userQuery = useQuery({ queryKey: ['user', userId], queryFn: () => api.users.getUser(userId) });
  const rolesQuery = useQuery({ queryKey: ['user', userId, 'roles'], queryFn: () => api.users.getUserRoles(userId) });
  const groupsQuery = useQuery({ queryKey: ['user', userId, 'groups'], queryFn: () => api.users.getUserGroups(userId) });
  const identitiesQuery = useQuery({
    queryKey: ['user', userId, 'identities'],
    queryFn: () => api.users.getUserUpstreamIdentities(userId),
  });
  const allRolesQuery = useQuery({
    queryKey: ['roles', 'all'],
    queryFn: () => api.roles.getRoles({ page: 1, pageSize: 100 }),
    enabled: canAssignRoles,
  });

  const invalidateRoles = () => void queryClient.invalidateQueries({ queryKey: ['user', userId, 'roles'] });

  const assignRole = useMutation({
    mutationFn: (roleId: string) => api.users.assignUserRole(userId, roleId),
    onSuccess: () => {
      notifications.show({ message: 'Role assigned', color: 'green' });
      setRoleToAssign(null);
      invalidateRoles();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const unassignRole = useMutation({
    mutationFn: (roleId: string) => api.users.unassignUserRole(userId, roleId),
    onSuccess: () => {
      notifications.show({ message: 'Role removed', color: 'green' });
      invalidateRoles();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const canManageProviders = canWrite && hasPermission(auth.permissions, 'providers:read');
  const [linkProviderId, setLinkProviderId] = useState<string | null>(null);
  const [linkSubject, setLinkSubject] = useState('');

  const providersQuery = useQuery({
    queryKey: ['providers', 'all'],
    queryFn: () => api.providers.getProviders(true),
    enabled: canManageProviders,
  });

  const invalidateIdentities = () => void queryClient.invalidateQueries({ queryKey: ['user', userId, 'identities'] });

  const linkIdentity = useMutation({
    mutationFn: () =>
      api.users.linkUserUpstreamIdentity(userId, { providerId: linkProviderId ?? '', subject: linkSubject.trim() }),
    onSuccess: () => {
      notifications.show({ message: 'Identity linked', color: 'green' });
      setLinkProviderId(null);
      setLinkSubject('');
      invalidateIdentities();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const unlinkIdentity = useMutation({
    mutationFn: (providerId: string) => api.users.unlinkUserUpstreamIdentity(userId, providerId),
    onSuccess: () => {
      notifications.show({ message: 'Identity unlinked', color: 'green' });
      invalidateIdentities();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
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
          <SectionCard
            title="Assigned roles"
            description="Platform roles assigned directly to this user. Members inherit the permissions of every assigned role."
            right={
              canAssignRoles ? (
                <Group gap="xs" wrap="nowrap">
                  <Select
                    placeholder="Select a role"
                    searchable
                    w={200}
                    data={(allRolesQuery.data?.items ?? [])
                      .filter((role) => !roles.some((assigned) => assigned.id === role.id))
                      .map((role) => ({ value: role.id, label: role.displayName }))}
                    value={roleToAssign}
                    onChange={setRoleToAssign}
                  />
                  <Button
                    disabled={!roleToAssign}
                    loading={assignRole.isPending}
                    onClick={() => roleToAssign && assignRole.mutate(roleToAssign)}
                  >
                    Assign
                  </Button>
                </Group>
              ) : undefined
            }
          >
            {roles.length === 0 ? (
              <Text c="dimmed" size="sm">
                No roles assigned.
              </Text>
            ) : (
              <Stack gap={0}>
                {roles.map((role, index) => (
                  <Group
                    key={role.id}
                    justify="space-between"
                    wrap="nowrap"
                    py="sm"
                    style={{ borderBottom: index === roles.length - 1 ? undefined : '1px solid var(--mw-border)' }}
                  >
                    <Group gap="sm" wrap="nowrap">
                      <Badge color="blue" variant="light">
                        {role.displayName}
                      </Badge>
                      {role.isSystemRole && (
                        <Text c="dimmed" size="xs">
                          System role
                        </Text>
                      )}
                    </Group>
                    {canAssignRoles && (
                      <ActionIcon
                        aria-label={`Remove ${role.displayName}`}
                        color="red"
                        variant="subtle"
                        loading={unassignRole.isPending && unassignRole.variables === role.id}
                        onClick={() => unassignRole.mutate(role.id)}
                      >
                        <Icon name="x" size={16} />
                      </ActionIcon>
                    )}
                  </Group>
                ))}
              </Stack>
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
            {canManageProviders && (providersQuery.data?.length ?? 0) > 0 && (
              <Group align="flex-end" gap="sm" mb="md" wrap="nowrap">
                <Select
                  label="Provider"
                  placeholder="Select a provider"
                  w={200}
                  data={(providersQuery.data ?? []).map((provider) => ({
                    value: provider.id,
                    label: provider.displayName || provider.name,
                  }))}
                  value={linkProviderId}
                  onChange={setLinkProviderId}
                />
                <TextInput
                  label="Subject"
                  placeholder="upstream-subject-id"
                  style={{ flex: 1, fontFamily: 'var(--mw-mono)' }}
                  value={linkSubject}
                  onChange={(event) => setLinkSubject(event.currentTarget.value)}
                />
                <Button
                  leftSection={<Icon name="link" size={15} />}
                  disabled={!linkProviderId || !linkSubject.trim()}
                  loading={linkIdentity.isPending}
                  onClick={() => linkIdentity.mutate()}
                >
                  Link
                </Button>
              </Group>
            )}
            {identities.length === 0 ? (
              <Text c="dimmed" size="sm">
                No linked upstream identities.
              </Text>
            ) : (
              <Stack gap={0}>
                {identities.map((identity, index) => (
                  <Group
                    key={`${identity.providerId}-${identity.subject}`}
                    justify="space-between"
                    wrap="nowrap"
                    py="sm"
                    style={{ borderBottom: index === identities.length - 1 ? undefined : '1px solid var(--mw-border)' }}
                  >
                    <Box style={{ minWidth: 0 }}>
                      <Text fw={600} size="sm">
                        {identity.providerName ?? identity.providerId}
                      </Text>
                      <Text c="dimmed" className="mw-mono" size="xs" truncate>
                        {identity.subject}
                      </Text>
                    </Box>
                    {canWrite && (
                      <ActionIcon
                        aria-label={`Unlink ${identity.providerName ?? identity.providerId}`}
                        color="red"
                        variant="subtle"
                        loading={unlinkIdentity.isPending && unlinkIdentity.variables === identity.providerId}
                        onClick={() => unlinkIdentity.mutate(identity.providerId)}
                      >
                        <Icon name="unlink" size={16} />
                      </ActionIcon>
                    )}
                  </Group>
                ))}
              </Stack>
            )}
          </SectionCard>
        </Tabs.Panel>
      </Tabs>
    </div>
  );
}
