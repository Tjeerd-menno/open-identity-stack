import { Avatar, Box, Button, Group, Modal, PasswordInput, Stack, Text, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import type { UserListItem } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterToolbar } from '@/components/FilterToolbar';
import { Pager } from '@/components/ListControls';
import { RowMenu } from '@/components/RowMenu';
import { ConfirmModal } from '@/components/ConfirmModal';
import { ErrorState, PageHeader, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import { formatRelativeTime, getInitials } from '@/lib/format';

export function UsersPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(auth.permissions, 'users:write');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [createOpened, createControls] = useDisclosure(false);
  const [pendingDelete, setPendingDelete] = useState<UserListItem | null>(null);

  const query = useQuery({
    queryKey: ['users', { page, pageSize, search }],
    queryFn: () => api.users.getUsers({ page, pageSize, search: search || undefined }),
  });

  const setStatus = useMutation({
    mutationFn: (user: UserListItem) =>
      user.status === 'Disabled'
        ? api.users.enableUser(user.id)
        : api.users.disableUser(user.id, { reason: 'Disabled from Management Web' }),
    onSuccess: () => {
      notifications.show({ message: 'User status updated', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['users'] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const deleteUser = useMutation({
    mutationFn: (user: UserListItem) => api.users.deleteUser(user.id),
    onSuccess: () => {
      notifications.show({ message: 'User deleted', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['users'] });
      setPendingDelete(null);
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const columns: Column<UserListItem>[] = [
    {
      key: 'user',
      header: 'User',
      render: (user) => (
        <Group gap="sm" wrap="nowrap">
          <Avatar color="blue" name={user.displayName} size={36} radius="xl">
            {getInitials(user.displayName)}
          </Avatar>
          <Box style={{ minWidth: 0 }}>
            <Text fw={600} size="sm" truncate>
              {user.displayName}
            </Text>
            <Text c="dimmed" size="xs" truncate>
              {user.email}
            </Text>
          </Box>
        </Group>
      ),
    },
    { key: 'status', header: 'Status', render: (user) => <StatusBadge status={user.status} /> },
    {
      key: 'created',
      header: 'Created',
      render: (user) => (
        <Text c="dimmed" size="sm">
          {formatRelativeTime(user.createdAt)}
        </Text>
      ),
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (user) => (
        <RowMenu
          items={[
            { label: 'Open', icon: 'arrow-up-right', onClick: () => navigate(`/users/${user.id}`) },
            {
              label: user.status === 'Disabled' ? 'Enable user' : 'Disable user',
              icon: 'ban',
              disabled: !canWrite,
              onClick: () => setStatus.mutate(user),
            },
            { separator: true },
            { label: 'Delete user', icon: 'trash-2', danger: true, disabled: !canWrite, onClick: () => setPendingDelete(user) },
          ]}
        />
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Users"
        description="Accounts, status, roles, groups and the upstream identities you administer."
        actions={
          canWrite ? (
            <Button leftSection={<Icon name="user-plus" size={16} />} onClick={createControls.open}>
              Add user
            </Button>
          ) : undefined
        }
      />

      <FilterToolbar
        noun="users"
        search={search}
        onSearch={(value) => {
          setSearch(value);
          setPage(1);
        }}
        rows={pageSize}
        onRows={(size) => {
          setPageSize(size);
          setPage(1);
        }}
        count={query.data?.totalCount}
      />

      {query.isError ? (
        <ErrorState message={getApiErrorMessage(query.error)} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={query.data?.items ?? []}
            getRowKey={(user) => user.id}
            onRowClick={(user) => navigate(`/users/${user.id}`)}
            isLoading={query.isLoading}
            emptyIcon="users"
            emptyTitle="No users found"
            emptyText="Adjust your search or add a user."
          />
          <Pager
            page={page}
            pageSize={pageSize}
            totalCount={query.data?.totalCount ?? 0}
            totalPages={query.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setPage(1);
            }}
          />
        </>
      )}

      {createOpened && <CreateUserModal onClose={createControls.close} />}
      <ConfirmModal
        opened={pendingDelete !== null}
        title="Delete user"
        message={`Permanently delete ${pendingDelete?.displayName ?? 'this user'}? This cannot be undone.`}
        confirmLabel="Delete user"
        loading={deleteUser.isPending}
        onConfirm={() => pendingDelete && deleteUser.mutate(pendingDelete)}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}

function CreateUserModal({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient();
  const form = useForm({
    initialValues: { email: '', displayName: '', password: '' },
    validate: {
      email: (value) => (/^\S+@\S+\.\S+$/.test(value) ? null : 'Enter a valid email'),
      displayName: (value) => (value.trim().length > 0 ? null : 'Required'),
      password: (value) => (value.length >= 12 ? null : 'Use at least 12 characters'),
    },
  });

  const mutation = useMutation({
    mutationFn: (values: typeof form.values) => api.users.createUser(values),
    onSuccess: (user) => {
      notifications.show({ message: `Created ${user.displayName}`, color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['users'] });
      onClose();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Modal opened onClose={onClose} title="Add user" centered>
      <form onSubmit={form.onSubmit((values) => mutation.mutate(values))}>
        <Stack gap="md">
          <TextInput label="Full name" placeholder="Grace Hopper" required {...form.getInputProps('displayName')} />
          <TextInput
            label="Email"
            placeholder="grace.hopper@example.com"
            leftSection={<Icon name="mail" size={16} />}
            required
            {...form.getInputProps('email')}
          />
          <PasswordInput
            label="Temporary password"
            description="The user should change this at first sign-in."
            required
            {...form.getInputProps('password')}
          />
          <Group justify="flex-end" gap="sm" mt="xs">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={mutation.isPending}>
              Create user
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
