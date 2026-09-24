import { ActionIcon, Badge, Box, Button, Group, Modal, PasswordInput, Select, Stack, Tabs, Text, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useParams } from 'react-router';
import type { RoleListItem, UpstreamIdentity, User, UserGroup, UserRoleListItem } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { BackLink, CenteredState, DetailHeader, ErrorState, FieldRow, MetaStrip, SectionCard, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import { formatDateTime, formatRelativeTime } from '@/lib/format';
import { useSyncedForm } from '@/lib/use-synced-form';

export function UserDetailPage() {
  const { userId = '' } = useParams();
  const auth = useAuth();
  const queryClient = useQueryClient();
  // Gate each action on the granular permission its backend endpoint actually requires,
  // rather than treating users:write as an umbrella (UsersApi authorizes disable, reset,
  // role assignment and group/role reads independently).
  const canWrite = hasPermission(auth.permissions, 'users:write');
  const canResetPassword = hasPermission(auth.permissions, 'users:reset-password');
  const canDisableUser = hasPermission(auth.permissions, 'users:disable');
  const canReadRoles = hasPermission(auth.permissions, 'roles:read');
  const canReadGroups = hasPermission(auth.permissions, 'groups:read');
  // Assignment is authorized with roles:assign; listing the roles to pick from needs roles:read.
  const canAssignRoles = hasPermission(auth.permissions, 'roles:assign');
  const [roleToAssign, setRoleToAssign] = useState<string | null>(null);
  const [resetOpened, resetControls] = useDisclosure(false);

  const userQuery = useQuery({ queryKey: ['user', userId], queryFn: () => api.users.getUser(userId) });
  const rolesQuery = useQuery({
    queryKey: ['user', userId, 'roles'],
    queryFn: () => api.users.getUserRoles(userId),
    enabled: canReadRoles,
  });
  const groupsQuery = useQuery({
    queryKey: ['user', userId, 'groups'],
    queryFn: () => api.users.getUserGroups(userId),
    enabled: canReadGroups,
  });
  const identitiesQuery = useQuery({
    queryKey: ['user', userId, 'identities'],
    queryFn: () => api.users.getUserUpstreamIdentities(userId),
  });
  const allRolesQuery = useQuery({
    queryKey: ['roles', 'all'],
    queryFn: () => api.roles.getRoles({ page: 1, pageSize: 100 }),
    enabled: canAssignRoles && canReadRoles,
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

  const invalidateIdentities = () => void queryClient.invalidateQueries({ queryKey: ['user', userId, 'identities'] });

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
    <UserDetailView
      user={user}
      userId={userId}
      canWrite={canWrite}
      canResetPassword={canResetPassword}
      canDisableUser={canDisableUser}
      canReadRoles={canReadRoles}
      canReadGroups={canReadGroups}
      canAssignRoles={canAssignRoles}
      roles={roles}
      groups={groups}
      identities={identities}
      availableRoles={allRolesQuery.data?.items ?? []}
      roleToAssign={roleToAssign}
      onRoleToAssign={setRoleToAssign}
      isAssigningRole={assignRole.isPending}
      onAssignRole={() => roleToAssign && assignRole.mutate(roleToAssign)}
      pendingRoleId={unassignRole.isPending ? unassignRole.variables : undefined}
      onUnassignRole={(roleId) => unassignRole.mutate(roleId)}
      isUnlinkingIdentity={unlinkIdentity.isPending}
      unlinkingProviderId={unlinkIdentity.isPending ? unlinkIdentity.variables : undefined}
      onUnlinkIdentity={(providerId) => unlinkIdentity.mutate(providerId)}
      isTogglingStatus={toggleStatus.isPending}
      onToggleStatus={() => toggleStatus.mutate()}
      resetOpened={resetOpened}
      onOpenReset={resetControls.open}
      onCloseReset={resetControls.close}
    />
  );
}

type UserDetailViewProps = {
  user: User;
  userId: string;
  canWrite: boolean;
  canResetPassword: boolean;
  canDisableUser: boolean;
  canReadRoles: boolean;
  canReadGroups: boolean;
  canAssignRoles: boolean;
  roles: UserRoleListItem[];
  groups: UserGroup[];
  identities: UpstreamIdentity[];
  availableRoles: RoleListItem[];
  roleToAssign: string | null;
  onRoleToAssign: (roleId: string | null) => void;
  isAssigningRole: boolean;
  onAssignRole: () => void;
  pendingRoleId: string | undefined;
  onUnassignRole: (roleId: string) => void;
  isUnlinkingIdentity: boolean;
  unlinkingProviderId: string | undefined;
  onUnlinkIdentity: (providerId: string) => void;
  isTogglingStatus: boolean;
  onToggleStatus: () => void;
  resetOpened: boolean;
  onOpenReset: () => void;
  onCloseReset: () => void;
};

function UserDetailView(props: UserDetailViewProps) {
  const { user, canWrite, canDisableUser, canReadRoles, canReadGroups, roles, groups } = props;
  // Enabling a disabled account is authorized with users:write; disabling needs users:disable.
  const canToggleStatus = user.status === 'Disabled' ? canWrite : canDisableUser;

  return (
    <div>
      <BackLink label="Back to users" to="/users" />
      <DetailHeader
        avatarName={user.displayName}
        title={user.displayName}
        description={user.email}
        badge={<StatusBadge status={user.status} />}
        actions={<UserHeaderActions {...props} canToggleStatus={canToggleStatus} />}
      />
      <MetaStrip items={getUserMetaItems(user, canReadRoles, canReadGroups, roles.length, groups.length)} />
      <UserDetailTabs {...props} />
      {props.resetOpened && <ResetPasswordModal userId={props.userId} userName={user.displayName} onClose={props.onCloseReset} />}
    </div>
  );
}

function UserHeaderActions({
  user,
  canResetPassword,
  canToggleStatus,
  isTogglingStatus,
  onOpenReset,
  onToggleStatus,
}: UserDetailViewProps & { canToggleStatus: boolean }) {
  if (!canResetPassword && !canToggleStatus) return undefined;

  return (
    <>
      {canResetPassword && <Button variant="default" leftSection={<Icon name="key-round" size={16} />} onClick={onOpenReset}>Reset password</Button>}
      {canToggleStatus && (
        <Button
          variant="default"
          loading={isTogglingStatus}
          leftSection={<Icon name={user.status === 'Disabled' ? 'power' : 'ban'} size={16} />}
          onClick={onToggleStatus}
        >
          {user.status === 'Disabled' ? 'Enable user' : 'Disable user'}
        </Button>
      )}
    </>
  );
}

function getUserMetaItems(user: User, canReadRoles: boolean, canReadGroups: boolean, roleCount: number, groupCount: number) {
  return [
    ...(canReadRoles ? [{ label: 'Roles', value: roleCount }] : []),
    ...(canReadGroups ? [{ label: 'Groups', value: groupCount }] : []),
    { label: 'MFA', value: user.mfaEnabled ? 'On' : 'Off' },
    { label: 'Created', value: formatDateTime(user.createdAt) },
  ];
}

function UserDetailTabs(props: UserDetailViewProps) {
  const { user, canWrite, canReadRoles, canReadGroups, roles, groups, identities } = props;

  return (
    <Tabs defaultValue="profile" color="blue" keepMounted={false}>
      <Tabs.List mb="lg">
        <Tabs.Tab value="profile">Profile</Tabs.Tab>
        {canReadRoles && <Tabs.Tab value="roles">Roles ({roles.length})</Tabs.Tab>}
        {canReadGroups && <Tabs.Tab value="groups">Groups ({groups.length})</Tabs.Tab>}
        <Tabs.Tab value="identities">Upstream identities ({identities.length})</Tabs.Tab>
      </Tabs.List>
      <Tabs.Panel value="profile"><ProfileCard user={user} canWrite={canWrite} /></Tabs.Panel>
      {canReadRoles && <Tabs.Panel value="roles"><UserRolesPanel {...props} /></Tabs.Panel>}
      {canReadGroups && <Tabs.Panel value="groups"><UserGroupsPanel groups={groups} /></Tabs.Panel>}
      <Tabs.Panel value="identities"><UserIdentitiesPanel {...props} /></Tabs.Panel>
    </Tabs>
  );
}

function UserRolesPanel({
  canAssignRoles,
  roles,
  availableRoles,
  roleToAssign,
  onRoleToAssign,
  isAssigningRole,
  onAssignRole,
  pendingRoleId,
  onUnassignRole,
}: UserDetailViewProps) {
  const assignableRoles = availableRoles.filter((role) => !roles.some((assigned) => assigned.id === role.id));

  return (
    <SectionCard
      title="Assigned roles"
      description="Platform roles assigned directly to this user. Members inherit the permissions of every assigned role."
      right={canAssignRoles ? (
        <Group gap="xs" wrap="nowrap">
          <Select
            placeholder="Select a role"
            searchable
            w={200}
            data={assignableRoles.map((role) => ({ value: role.id, label: role.displayName }))}
            value={roleToAssign}
            onChange={onRoleToAssign}
          />
          <Button disabled={!roleToAssign} loading={isAssigningRole} onClick={onAssignRole}>Assign</Button>
        </Group>
      ) : undefined}
    >
      {roles.length === 0 ? (
        <Text c="dimmed" size="sm">No roles assigned.</Text>
      ) : (
        <Stack gap={0}>
          {roles.map((role, index) => (
            <Group key={role.id} justify="space-between" wrap="nowrap" py="sm" style={{ borderBottom: index === roles.length - 1 ? undefined : '1px solid var(--mw-border)' }}>
              <Group gap="sm" wrap="nowrap">
                <Badge color="blue" variant="light">{role.displayName}</Badge>
                {role.isSystemRole && <Text c="dimmed" size="xs">System role</Text>}
              </Group>
              {canAssignRoles && (
                <ActionIcon
                  aria-label={`Remove ${role.displayName}`}
                  color="red"
                  variant="subtle"
                  loading={pendingRoleId === role.id}
                  onClick={() => onUnassignRole(role.id)}
                >
                  <Icon name="x" size={16} />
                </ActionIcon>
              )}
            </Group>
          ))}
        </Stack>
      )}
    </SectionCard>
  );
}

function UserGroupsPanel({ groups }: { groups: UserGroup[] }) {
  return (
    <SectionCard title="Group membership">
      {groups.length === 0 ? (
        <Text c="dimmed" size="sm">Not a member of any group.</Text>
      ) : (
        <Stack gap={0}>
          {groups.map((group, index) => (
            <FieldRow key={group.id} label={group.name} value={group.description ?? '—'} last={index === groups.length - 1} />
          ))}
        </Stack>
      )}
    </SectionCard>
  );
}

function UserIdentitiesPanel({ identities, canWrite, isUnlinkingIdentity, unlinkingProviderId, onUnlinkIdentity }: UserDetailViewProps) {
  return (
    <SectionCard title="Upstream identities" description="Federated accounts linked to this user.">
      <Text c="dimmed" size="sm" mb="md">Linking an existing account requires proof of account ownership. This workflow is not yet available.</Text>
      {identities.length === 0 ? (
        <Text c="dimmed" size="sm">No linked upstream identities.</Text>
      ) : (
        <Stack gap={0}>
          {identities.map((identity, index) => (
            <Group
              key={JSON.stringify([identity.providerId, identity.subjectId ?? identity.subject])}
              justify="space-between"
              wrap="nowrap"
              py="sm"
              style={{ borderBottom: index === identities.length - 1 ? undefined : '1px solid var(--mw-border)' }}
            >
              <Box style={{ minWidth: 0 }}>
                <Text fw={600} size="sm">{identity.providerName ?? identity.providerId}</Text>
                <Text c="dimmed" className="mw-mono" size="xs" truncate>{identity.subjectId ?? identity.subject}</Text>
                <Text size="xs" c={identity.isQuarantined !== false ? 'red' : 'dimmed'}>
                  {identity.isQuarantined !== false ? 'Quarantined — authentication and migration blocked' : 'Association evidence recorded'}
                </Text>
                <Text size="xs" c="dimmed">Evidence: {identity.associationEvidence ?? 'Unknown'}</Text>
              </Box>
              {canWrite && identity.isQuarantined === false && (
                <ActionIcon
                  aria-label={`Unlink ${identity.providerName ?? identity.providerId}`}
                  color="red"
                  variant="subtle"
                  loading={isUnlinkingIdentity && unlinkingProviderId === identity.providerId}
                  onClick={() => onUnlinkIdentity(identity.providerId)}
                >
                  <Icon name="unlink" size={16} />
                </ActionIcon>
              )}
            </Group>
          ))}
        </Stack>
      )}
    </SectionCard>
  );
}

function ProfileCard({ user, canWrite }: { user: User; canWrite: boolean }) {
  const queryClient = useQueryClient();
  const form = useForm({
    initialValues: { displayName: user.displayName },
    validate: { displayName: (value) => (value.trim() ? null : 'Required') },
  });

  useSyncedForm(form, { displayName: user.displayName }, user.id);

  const save = useMutation({
    mutationFn: (values: typeof form.values) => api.users.updateUser(user.id, { displayName: values.displayName }),
    onSuccess: () => {
      notifications.show({ message: 'User updated', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['user', user.id] });
      void queryClient.invalidateQueries({ queryKey: ['users'] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <SectionCard title="Profile">
      <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
        <Stack gap="md">
          <TextInput label="Display name" disabled={!canWrite} {...form.getInputProps('displayName')} />
          {canWrite && (
            <Group justify="flex-end">
              <Button type="submit" loading={save.isPending} disabled={!form.isDirty()}>
                Save changes
              </Button>
            </Group>
          )}
        </Stack>
      </form>
      <Stack gap={0} mt="md">
        <FieldRow label="User ID" value={user.id} mono />
        <FieldRow label="Email" value={user.email} />
        <FieldRow label="Email verification" value={user.emailVerified ? 'Verified' : 'No current verification evidence'} />
        {(user.emailVerificationEvidence ?? []).map((evidence) => (
          <FieldRow key={JSON.stringify([evidence.providerId, evidence.issuer, evidence.email, evidence.verifiedAt])}
            label={evidence.providerId ? 'Provider evidence' : 'Independent evidence'}
            value={`${evidence.providerId ? `Provider ${evidence.providerId} · ` : ''}${evidence.issuer ?? 'Local email verification'} · ${formatDateTime(evidence.verifiedAt)}${evidence.withdrawnAt ? ' · Withdrawn' : ''}`} />
        ))}
        <FieldRow label="MFA" value={user.mfaEnabled ? 'Enabled' : 'Not enabled'} />
        <FieldRow label="Last sign-in" value={formatRelativeTime(user.lastLoginAt)} />
        <FieldRow label="Created" value={formatDateTime(user.createdAt)} last />
      </Stack>
    </SectionCard>
  );
}

function ResetPasswordModal({ userId, userName, onClose }: { userId: string; userName: string; onClose: () => void }) {
  const form = useForm({
    initialValues: { newPassword: '' },
    validate: { newPassword: (value) => (value.length >= 12 ? null : 'Use at least 12 characters') },
  });

  const mutation = useMutation({
    mutationFn: (values: typeof form.values) => api.users.resetUserPassword(userId, { newPassword: values.newPassword }),
    onSuccess: (response) => {
      notifications.show({
        message: response.temporaryPassword ? 'Password reset — share the temporary password' : 'Password reset',
        color: 'green',
      });
      onClose();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Modal opened onClose={onClose} title="Reset password" centered>
      <form onSubmit={form.onSubmit((values) => mutation.mutate(values))}>
        <Stack gap="md">
          <Text c="dimmed" size="sm">
            Set a new temporary password for {userName}. They should change it at next sign-in.
          </Text>
          <PasswordInput label="New temporary password" required {...form.getInputProps('newPassword')} />
          <Group justify="flex-end" gap="sm" mt="xs">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={mutation.isPending}>
              Reset password
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
