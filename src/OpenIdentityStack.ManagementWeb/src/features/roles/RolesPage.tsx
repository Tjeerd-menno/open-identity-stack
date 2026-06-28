import { Badge, Box, Button, Group, Modal, Stack, Text, TextInput, Textarea, ThemeIcon } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import type { RoleListItem } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterToolbar } from '@/components/FilterToolbar';
import { Pager } from '@/components/ListControls';
import { RowMenu } from '@/components/RowMenu';
import { ConfirmModal } from '@/components/ConfirmModal';
import { ErrorState, PageHeader } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';

export function RolesPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(auth.permissions, 'roles:write');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [createOpened, createControls] = useDisclosure(false);
  const [pendingDelete, setPendingDelete] = useState<RoleListItem | null>(null);

  const query = useQuery({
    queryKey: ['roles', { page, pageSize, search }],
    queryFn: () => api.roles.getRoles({ page, pageSize, search: search || undefined }),
  });

  const remove = useMutation({
    mutationFn: (role: RoleListItem) => api.roles.deleteRole(role.id),
    onSuccess: () => {
      notifications.show({ message: 'Role deleted', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['roles'] });
      setPendingDelete(null);
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const columns: Column<RoleListItem>[] = [
    {
      key: 'role',
      header: 'Role',
      render: (role) => (
        <Group gap="sm" wrap="nowrap">
          <ThemeIcon color="blue" radius="md" size={36} variant="light">
            <Icon name="shield" size={18} />
          </ThemeIcon>
          <Box style={{ minWidth: 0 }}>
            <Group gap="xs">
              <Text fw={600} size="sm">{role.displayName}</Text>
              {role.isSystemRole && <Badge color="gray" size="sm" variant="outline">System</Badge>}
            </Group>
            <Text c="dimmed" className="mw-mono" size="xs">{role.name}</Text>
          </Box>
        </Group>
      ),
    },
    {
      key: 'description',
      header: 'Description',
      render: (role) => <Text c="dimmed" size="sm" lineClamp={1}>{role.description ?? '—'}</Text>,
    },
    {
      key: 'permissions',
      header: 'Permissions',
      align: 'right',
      render: (role) => <Badge color="blue" variant="light">{role.permissions.length}</Badge>,
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (role) => (
        <RowMenu
          items={[
            { label: 'Open', icon: 'arrow-up-right', onClick: () => navigate(`/roles/${role.id}`) },
            { separator: true },
            {
              label: 'Delete role',
              icon: 'trash-2',
              danger: true,
              disabled: !canWrite || role.isSystemRole,
              onClick: () => setPendingDelete(role),
            },
          ]}
        />
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Roles"
        description="Roles grant platform permissions. Users and groups inherit the permissions of the roles they hold."
        actions={
          canWrite ? (
            <Button leftSection={<Icon name="plus" size={16} />} onClick={createControls.open}>
              Create role
            </Button>
          ) : undefined
        }
      />

      <FilterToolbar
        noun="roles"
        search={search}
        onSearch={(value) => { setSearch(value); setPage(1); }}
        rows={pageSize}
        onRows={(size) => { setPageSize(size); setPage(1); }}
        count={query.data?.totalCount}
      />

      {query.isError ? (
        <ErrorState message={getApiErrorMessage(query.error)} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={query.data?.items ?? []}
            getRowKey={(role) => role.id}
            onRowClick={(role) => navigate(`/roles/${role.id}`)}
            isLoading={query.isLoading}
            emptyIcon="shield"
            emptyTitle="No roles"
            emptyText="Adjust your search or create a role."
          />
          <Pager
            page={page}
            pageSize={pageSize}
            totalCount={query.data?.totalCount ?? 0}
            totalPages={query.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
          />
        </>
      )}

      {createOpened && <CreateRoleModal onClose={createControls.close} />}
      <ConfirmModal
        opened={pendingDelete !== null}
        title="Delete role"
        message={`Delete ${pendingDelete?.displayName ?? 'this role'}? Members lose the permissions it grants.`}
        confirmLabel="Delete role"
        loading={remove.isPending}
        onConfirm={() => pendingDelete && remove.mutate(pendingDelete)}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}

function CreateRoleModal({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient();
  const form = useForm({
    initialValues: { name: '', displayName: '', description: '' },
    validate: {
      name: (value) => (/^[a-z0-9-]+$/.test(value) ? null : 'Lowercase letters, numbers and hyphens only'),
      displayName: (value) => (value.trim() ? null : 'Required'),
    },
  });

  const mutation = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.roles.createRole({
        name: values.name,
        displayName: values.displayName,
        description: values.description || null,
        permissions: [],
        acknowledgeWildcardGrant: false,
      }),
    onSuccess: (role) => {
      notifications.show({ message: `Created ${role.displayName}`, color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['roles'] });
      onClose();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Modal opened onClose={onClose} title="Create role" centered>
      <form onSubmit={form.onSubmit((values) => mutation.mutate(values))}>
        <Stack gap="md">
          <TextInput label="Role name" placeholder="billing-admin" required {...form.getInputProps('name')} />
          <TextInput label="Display name" placeholder="Billing admin" required {...form.getInputProps('displayName')} />
          <Textarea label="Description" placeholder="Manages billing applications and credentials." autosize minRows={2} {...form.getInputProps('description')} />
          <Text c="dimmed" size="xs">Permissions are assigned to the role after creation from the role detail page.</Text>
          <Group justify="flex-end" gap="sm" mt="xs">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={mutation.isPending}>Create role</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
