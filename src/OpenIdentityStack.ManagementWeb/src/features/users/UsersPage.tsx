import { Alert, Button, Grid, Group, Loader, PasswordInput, Stack, Text, TextInput, Title } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { ActionBar, FormSection } from '@/components/FormPrimitives';
import { FoundationTable } from '@/components/FoundationTable';
import { listRoles } from '@/lib/admin-api';
import { hasPermission } from '@/lib/permissions';
import { UserDetailsPanel } from './UserDetailsPanel';
import { useCreateUserMutation } from './user-mutations';
import { getUser, getUserGroups, getUserRoles, getUsers, getUserUpstreamIdentities, type UserListItem } from './users-api';

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
  const canDeleteUsers = hasPermission(permissions, 'users:delete');
  const canResetUserPasswords = hasPermission(permissions, 'users:reset-password');
  const canAssignRoles = hasPermission(permissions, 'roles:assign');
  const canReadGroups = hasPermission(permissions, 'groups:read');
  const users = useQuery({
    queryKey: ['users', 'list', submittedSearch, currentPage],
    queryFn: () => getUsers({ page: currentPage, pageSize: 20, search: submittedSearch || undefined }),
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
    queryFn: () => getUserRoles(selectedUser?.id ?? ''),
    enabled: !!selectedUser,
  });
  const groups = useQuery({
    queryKey: ['users', selectedUser?.id, 'groups'],
    queryFn: () => getUserGroups(selectedUser?.id ?? ''),
    enabled: !!selectedUser && canReadGroups,
  });
  const upstreamIdentities = useQuery({
    queryKey: ['users', selectedUser?.id, 'upstream-identities'],
    queryFn: () => getUserUpstreamIdentities(selectedUser?.id ?? ''),
    enabled: !!selectedUser,
  });

  if (users.isLoading) {
    return <Loader aria-label="Loading users" />;
  }

  if (users.isError) {
    return <Alert color="red">Unable to load users.</Alert>;
  }

  const userItems = users.data?.items ?? [];
  const userColumns = [
    {
      header: 'User',
      cell: (user: UserListItem) => (
        <Stack gap={2}>
          <Button variant="subtle" onClick={() => setSelectedUser(user)}>
            {user.displayName}
          </Button>
          <Text size="sm" c="dimmed">
            {user.email}
          </Text>
        </Stack>
      ),
    },
    {
      header: 'Status',
      accessorKey: 'status' as const,
    },
  ];

  const submitCreateUser = async () => {
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
  };

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
        <FormSection title="Create user" description="Create a Management Web user account with an initial password.">
          <TextInput label="Email" value={newEmail} onChange={(event) => setNewEmail(event.currentTarget.value)} />
          <TextInput label="New display name" value={newDisplayName} onChange={(event) => setNewDisplayName(event.currentTarget.value)} />
          <PasswordInput label="Password" value={newPassword} onChange={(event) => setNewPassword(event.currentTarget.value)} />
          <ActionBar
            submitLabel="Save new user"
            cancelLabel="Close"
            isSubmitting={createUser.isPending}
            onSubmit={submitCreateUser}
            onCancel={() => {
              setShowCreateForm(false);
              setCreateError(null);
            }}
          />
          {createError && <Alert color="red">{createError}</Alert>}
        </FormSection>
      )}
      <Grid>
        <Grid.Col span={{ base: 12, md: 6 }}>
          <FoundationTable
            columns={userColumns}
            data={userItems}
            emptyMessage="No users found"
            pagination={users.data ? {
              page: users.data.page,
              pageSize: users.data.pageSize,
              totalCount: users.data.totalCount,
              totalPages: users.data.totalPages,
              onPageChange: setCurrentPage,
            } : undefined}
          />
        </Grid.Col>
        <Grid.Col span={{ base: 12, md: 6 }}>
          {!selectedUser && <Alert>Select a user to inspect details.</Alert>}
          {selectedUser
            && (details.isLoading
              || roles.isLoading
              || assignedRoles.isLoading
              || (canReadGroups && groups.isLoading)
              || upstreamIdentities.isLoading)
            && (
            <Loader aria-label="Loading user details" />
          )}
          {details.data && roles.data && assignedRoles.data && upstreamIdentities.data && (!canReadGroups || groups.data) && (
            <UserDetailsPanel
              user={details.data}
              availableRoles={roles.data.items}
              assignedRoles={assignedRoles.data}
              groups={groups.data ?? []}
              upstreamIdentities={upstreamIdentities.data}
              canUpdate={canWriteUsers}
              canDisable={canDisableUsers}
              canDelete={canDeleteUsers}
              canResetPassword={canResetUserPasswords}
              canAssignRoles={canAssignRoles}
              canReadGroups={canReadGroups}
            />
          )}
        </Grid.Col>
      </Grid>
    </Stack>
  );
}
