import { Alert, Badge, Card, Group, Stack, Text, Title } from '@mantine/core';
import type { RoleListItem, UpstreamIdentity, User, UserGroup } from './users-api';
import { UserEditForm } from './UserEditForm';

type UserDetailsPanelProps = {
  user: User;
  assignedRoles: RoleListItem[];
  availableRoles: RoleListItem[];
  groups: UserGroup[];
  upstreamIdentities: UpstreamIdentity[];
  canUpdate: boolean;
  canDisable: boolean;
  canDelete: boolean;
  canResetPassword: boolean;
  canAssignRoles: boolean;
  canReadGroups: boolean;
};

export function UserDetailsPanel({
  user,
  assignedRoles,
  availableRoles,
  groups,
  upstreamIdentities,
  canUpdate,
  canDisable,
  canDelete,
  canResetPassword,
  canAssignRoles,
  canReadGroups,
}: UserDetailsPanelProps) {
  return (
    <Card withBorder shadow="sm" radius="md" p="lg" role="region" aria-label="User details">
      <Stack gap="md">
        <div>
          <Title order={2}>{user.displayName}</Title>
          <Text c="dimmed">{user.email}</Text>
        </div>
        <Group>
          <Badge color={user.status === 'Active' ? 'green' : 'gray'}>{user.status}</Badge>
          {user.mfaEnabled && <Badge color="blue">MFA enabled</Badge>}
        </Group>
        <Text size="sm">Assigned roles: {assignedRoles.length > 0 ? assignedRoles.map((role) => role.displayName).join(', ') : 'None'}</Text>
        {canReadGroups && (
          <Stack gap={4}>
            <Text fw={600}>Groups</Text>
            {groups.length > 0 ? (
              groups.map((group) => (
                <Text key={group.id} size="sm">
                  {group.displayName ?? group.name}
                  {typeof group.memberCount === 'number' ? ` (${group.memberCount} members)` : ''}
                </Text>
              ))
            ) : (
              <Text size="sm" c="dimmed">No groups</Text>
            )}
          </Stack>
        )}
        <Stack gap={4}>
          <Text fw={600}>Upstream identities</Text>
          {upstreamIdentities.length > 0 ? (
            upstreamIdentities.map((identity) => (
              <Text key={identity.providerId} size="sm">
                {identity.providerName ?? identity.providerId}: {identity.subject}
              </Text>
            ))
          ) : (
            <Text size="sm" c="dimmed">No linked accounts</Text>
          )}
        </Stack>
        {canUpdate ? (
          <UserEditForm
            user={user}
            assignedRoles={assignedRoles}
            upstreamIdentities={upstreamIdentities}
            availableRoles={availableRoles}
            canDisable={canDisable}
            canDelete={canDelete}
            canResetPassword={canResetPassword}
            canAssignRoles={canAssignRoles}
          />
        ) : (
          <Alert color="yellow">Read-only access. You do not have permission to modify users.</Alert>
        )}
      </Stack>
    </Card>
  );
}
