import { Alert, Badge, Card, Group, Stack, Text, Title } from '@mantine/core';
import type { RoleListItem, User } from '@/lib/admin-api';
import { UserEditForm } from './UserEditForm';

type UserDetailsPanelProps = {
  user: User;
  assignedRoles: RoleListItem[];
  availableRoles: RoleListItem[];
  canUpdate: boolean;
  canDisable: boolean;
  canAssignRoles: boolean;
};

export function UserDetailsPanel({ user, assignedRoles, availableRoles, canUpdate, canDisable, canAssignRoles }: UserDetailsPanelProps) {
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
        {canUpdate ? (
          <UserEditForm
            user={user}
            availableRoles={availableRoles}
            canDisable={canDisable}
            canAssignRoles={canAssignRoles}
          />
        ) : (
          <Alert color="yellow">Read-only access. You do not have permission to modify users.</Alert>
        )}
      </Stack>
    </Card>
  );
}
