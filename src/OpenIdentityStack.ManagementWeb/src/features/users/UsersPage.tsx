import { Alert, Button, Card, Grid, Group, Loader, Pagination, PasswordInput, Stack, Table, Text, TextInput, Title } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { getUser, listRoles, listUserRoles, listUsers, type UserListItem } from '@/lib/admin-api';
import { hasPermission } from '@/lib/permissions';
import { UserDetailsPanel } from './UserDetailsPanel';
import { useCreateUserMutation } from './user-mutations';

type UsersPageProps = {
  permissions?: string[];
};

export function UsersPage({ permissions = ['*'] }: UsersPageProps) {
  const [selectedUser, setSelectedUser] = useState<UserListItem | null>(null);
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [newEmail, setNewEmail] = useState('');
  const [newDisplayName, setNewDisplayName] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [createError, setCreateError] = useState<string | null>(null);
  const createUser = useCreateUserMutation();
  const canWriteUsers = hasPermission(permissions, 'users:write');
  const canDisableUsers = hasPermission(permissions, 'users:disable');
  const canAssignRoles = hasPermission(permissions, 'roles:assign');
  const users = useQuery({
    queryKey: ['users', 'list', submittedSearch, currentPage],
    queryFn: () => listUsers(currentPage, 20, submittedSearch),
  });
  const details = useQuery({
    queryKey: ['users', selectedUser?.id],
    queryFn: () => getUser(selectedUser?.id ?? ''),
    enabled: !!selectedUser,
  });
  const roles = useQuery({
    queryKey: ['roles', 'list'],
    queryFn: () => listRoles(),
    enabled: !!selectedUser,
  });
  const assignedRoles = useQuery({
    queryKey: ['users', selectedUser?.id, 'roles'],
    queryFn: () => listUserRoles(selectedUser?.id ?? ''),
    enabled: !!selectedUser,
  });

  if (users.isLoading) {
    return <Loader aria-label="Loading users" />;
  }

  if (users.isError) {
    return <Alert color="red">Unable to load users.</Alert>;
  }

  const userItems = users.data?.items ?? [];

  return (
    <Stack gap="lg">
      <Group justify="space-between">
        <div>
          <Title order={1}>Users</Title>
          <Text c="dimmed">Operate user accounts, status, and role assignment from Management Web.</Text>
        </div>
        {canWriteUsers && (
          <Button onClick={() => setShowCreateForm((value) => !value)}>
            Create user
          </Button>
        )}
      </Group>
      {!canWriteUsers && <Alert color="yellow">Read-only access. You do not have permission to modify users.</Alert>}
      <Group component="form" onSubmit={(event) => {
        event.preventDefault();
        setSubmittedSearch(search);
        setCurrentPage(1);
      }}>
        <TextInput label="Search users" value={search} onChange={(event) => setSearch(event.currentTarget.value)} />
        <Button type="submit">Search</Button>
      </Group>
      {showCreateForm && (
        <Card withBorder shadow="sm" radius="md">
          <Stack gap="md">
            <TextInput label="Email" value={newEmail} onChange={(event) => setNewEmail(event.currentTarget.value)} />
            <TextInput label="New display name" value={newDisplayName} onChange={(event) => setNewDisplayName(event.currentTarget.value)} />
            <PasswordInput label="Password" value={newPassword} onChange={(event) => setNewPassword(event.currentTarget.value)} />
            <Button
              onClick={async () => {
                setCreateError(null);
                try {
                  await createUser.mutateAsync({ email: newEmail, displayName: newDisplayName, password: newPassword });
                  setShowCreateForm(false);
                  setNewEmail('');
                  setNewDisplayName('');
                  setNewPassword('');
                } catch (mutationError) {
                  setCreateError(mutationError instanceof Error ? mutationError.message : 'Unable to create user.');
                }
              }}
              loading={createUser.isPending}
            >
              Save new user
            </Button>
            {createError && <Alert color="red">{createError}</Alert>}
          </Stack>
        </Card>
      )}
      <Grid>
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Card withBorder shadow="sm" radius="md">
            <Stack gap="md">
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>User</Table.Th>
                    <Table.Th>Status</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {userItems.map((user) => (
                    <Table.Tr key={user.id}>
                      <Table.Td>
                        <Button variant="subtle" onClick={() => setSelectedUser(user)}>
                          {user.displayName}
                        </Button>
                        <Text size="sm" c="dimmed">
                          {user.email}
                        </Text>
                      </Table.Td>
                      <Table.Td>{user.status}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
              {users.data && users.data.totalPages > 1 && (
                <Group justify="center">
                  <Pagination value={currentPage} onChange={setCurrentPage} total={users.data.totalPages} />
                </Group>
              )}
            </Stack>
          </Card>
        </Grid.Col>
        <Grid.Col span={{ base: 12, md: 6 }}>
          {!selectedUser && <Alert>Select a user to inspect details.</Alert>}
          {selectedUser && (details.isLoading || roles.isLoading || assignedRoles.isLoading) && (
            <Loader aria-label="Loading user details" />
          )}
          {details.data && roles.data && assignedRoles.data && (
            <UserDetailsPanel
              user={details.data}
              availableRoles={roles.data.items}
              assignedRoles={assignedRoles.data.roles}
              canUpdate={canWriteUsers}
              canDisable={canDisableUsers}
              canAssignRoles={canAssignRoles}
            />
          )}
        </Grid.Col>
      </Grid>
    </Stack>
  );
}
