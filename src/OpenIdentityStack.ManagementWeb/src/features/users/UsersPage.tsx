import { Alert, Avatar, Button, Group, Loader, PasswordInput, Stack, Text, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router';
import { BackLink } from '@/components/DetailPrimitives';
import { EntityActionGroup } from '@/components/EntityActionMenu';
import { FormSection } from '@/components/FormPrimitives';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
import { LoadingState, PageHeader, PageToolbar } from '@/components/PagePrimitives';
import { listRoles } from '@/lib/admin-api';
import { firstError, maxLength, minLength, required, validEmail } from '@/lib/form-validation';
import { hasPermission } from '@/lib/permissions';
import { UserDetailsPanel } from './UserDetailsPanel';
import { useCreateUserMutation } from './user-mutations';
import { getUser, getUserGroups, getUserRoles, getUsers, getUserUpstreamIdentities, type UserListItem } from './users-api';

type UsersPageProps = {
  permissions?: string[];
};

export function UsersPage({ permissions = ['*'] }: UsersPageProps) {
  const location = useLocation();
  const navigate = useNavigate();
  const { id } = useParams();
  const pathUserId = getUserIdFromPath(location.pathname);
  const selectedUserId = pathUserId ?? (id === 'create' ? undefined : id) ?? '';
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const searchMounted = useRef(false);
  const createUser = useCreateUserMutation();
  const createForm = useForm({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: {
      email: '',
      displayName: '',
      password: '',
    },
    validate: {
      email: (value) => firstError(
        required(value, 'Email is required.'),
        validEmail(value),
        maxLength(value, 256, 'Email')
      ),
      displayName: (value) => firstError(
        required(value, 'Display name is required.'),
        maxLength(value, 200, 'Display name')
      ),
      password: (value) => firstError(
        required(value, 'Password is required.'),
        minLength(value, 8, 'Password'),
        maxLength(value, 256, 'Password')
      ),
    },
  });
  const canWriteUsers = hasPermission(permissions, 'users:write');
  const canDisableUsers = hasPermission(permissions, 'users:disable');
  const canDeleteUsers = hasPermission(permissions, 'users:delete');
  const canResetUserPasswords = hasPermission(permissions, 'users:reset-password');
  const canAssignRoles = hasPermission(permissions, 'roles:assign');
  const canReadGroups = hasPermission(permissions, 'groups:read');
  const users = useQuery({
    queryKey: ['users', 'list', submittedSearch, currentPage],
    queryFn: () => getUsers({ page: currentPage, pageSize: 20, search: submittedSearch || undefined }),
    enabled: !selectedUserId,
  });
  const details = useQuery({
    queryKey: ['users', selectedUserId],
    queryFn: () => getUser(selectedUserId),
    enabled: !!selectedUserId,
  });
  const roles = useQuery({
    queryKey: ['roles', 'list'],
    queryFn: () => listRoles(),
    enabled: !!selectedUserId,
  });
  const assignedRoles = useQuery({
    queryKey: ['users', selectedUserId, 'roles'],
    queryFn: () => getUserRoles(selectedUserId),
    enabled: !!selectedUserId,
  });
  const groups = useQuery({
    queryKey: ['users', selectedUserId, 'groups'],
    queryFn: () => getUserGroups(selectedUserId),
    enabled: !!selectedUserId && canReadGroups,
  });
  const upstreamIdentities = useQuery({
    queryKey: ['users', selectedUserId, 'upstream-identities'],
    queryFn: () => getUserUpstreamIdentities(selectedUserId),
    enabled: !!selectedUserId,
  });

  useEffect(() => {
    if (selectedUserId) {
      return;
    }

    if (!searchMounted.current) {
      searchMounted.current = true;
      return;
    }

    const timer = window.setTimeout(() => {
      setSubmittedSearch(search);
      setCurrentPage(1);
    }, 300);

    return () => window.clearTimeout(timer);
  }, [search, selectedUserId]);

  if (selectedUserId) {
    return (
      <Stack gap="lg" role="region" aria-label="User detail page">
        <BackLink label="Back to Users" to="/users" />
        <PageHeader
          title="User details"
          description={selectedUserId}
        />
        {(details.isLoading
          || roles.isLoading
          || assignedRoles.isLoading
          || (canReadGroups && groups.isLoading)
          || upstreamIdentities.isLoading)
          && <Loader aria-label="Loading user details" />}
        {(details.isError || roles.isError || assignedRoles.isError || groups.isError || upstreamIdentities.isError) && (
          <Alert color="red">Unable to load user details.</Alert>
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
      </Stack>
    );
  }

  if (users.isLoading) {
    return (
      <LoadingState
        label="Loading users"
        title="Loading users"
        description="Retrieving user accounts and assignment summaries."
      />
    );
  }

  if (users.isError) {
    return <Alert color="red">Unable to load users.</Alert>;
  }

  const userItems = users.data?.items ?? [];
  const userColumns: FoundationColumn<UserListItem>[] = [
    {
      header: 'User',
      cell: (user: UserListItem) => (
        <Group gap="sm" wrap="nowrap">
          <Avatar name={user.displayName} color="initials" radius="xl" size={36} />
          <Stack gap={0} style={{ minWidth: 0 }}>
            <Button variant="subtle" px={4} mx={-4} onClick={() => navigate(`/users/${user.id}`)}>
              {user.displayName}
            </Button>
            <Text size="sm" c="dimmed">
              {user.email}
            </Text>
          </Stack>
        </Group>
      ),
    },
    {
      header: 'Status',
      accessorKey: 'status' as const,
    },
    {
      header: 'Actions',
      align: 'right',
      cell: (user) => (
        <EntityActionGroup
          actions={[
            { label: `View ${user.displayName}`, onClick: () => navigate(`/users/${user.id}`) },
            ...(canWriteUsers ? [{ label: `Edit ${user.displayName}`, onClick: () => navigate(`/users/${user.id}/edit`) }] : []),
          ]}
        />
      ),
    },
  ];

  const submitCreateUser = async (values: typeof createForm.values) => {
    setCreateError(null);
    try {
      await createUser.mutateAsync({
        email: values.email.trim(),
        displayName: values.displayName.trim(),
        password: values.password,
      });
      setShowCreateForm(false);
      createForm.reset();
    } catch (mutationError) {
      setCreateError(mutationError instanceof Error ? mutationError.message : 'Unable to create user.');
    }
  };

  return (
    <Stack gap="lg">
      <PageHeader
        title="Users"
        description="Operate user accounts, status, and role assignment from Management Web."
        actions={canWriteUsers && (
          <Button onClick={() => setShowCreateForm((value) => !value)}>
            Create user
          </Button>
        )}
      />
      {!canWriteUsers && <Alert color="yellow">Read-only access. You do not have permission to modify users.</Alert>}
      <PageToolbar
        searchLabel="Search users"
        searchPlaceholder="Search by email or display name..."
        searchValue={search}
        resultCount={users.data?.totalCount}
        onSearchChange={setSearch}
        onClear={() => {
          setSearch('');
          setSubmittedSearch('');
          setCurrentPage(1);
        }}
      />
      {showCreateForm && (
        <FormSection title="Create user" description="Create a Management Web user account with an initial password.">
          <form noValidate onSubmit={createForm.onSubmit((values) => void submitCreateUser(values))}>
            <Stack gap="sm">
              <TextInput aria-label="Email" label="Email" required {...createForm.getInputProps('email')} />
              <TextInput label="New display name" required {...createForm.getInputProps('displayName')} />
              <PasswordInput aria-label="Password" label="Password" required {...createForm.getInputProps('password')} />
              <Group justify="flex-end">
                <Button
                  type="button"
                  variant="default"
                  onClick={() => {
                    setShowCreateForm(false);
                    setCreateError(null);
                    createForm.reset();
                  }}
                >
                  Close
                </Button>
                <Button type="submit" loading={createUser.isPending}>
                  Save new user
                </Button>
              </Group>
              {createError && <Alert color="red">{createError}</Alert>}
            </Stack>
          </form>
        </FormSection>
      )}
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
    </Stack>
  );
}

function getUserIdFromPath(pathname: string) {
  const userId = /^\/users\/([^/]+)$/.exec(pathname)?.[1];
  return userId && userId !== 'create' ? userId : undefined;
}
