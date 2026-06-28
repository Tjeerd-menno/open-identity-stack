import { Badge, Box, Button, Group, Modal, Stack, Text, TextInput, Textarea, ThemeIcon } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import type { GroupListItem } from '@openidentitystack/admin-api-client';
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

export function GroupsPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(auth.permissions, 'groups:write');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [createOpened, createControls] = useDisclosure(false);
  const [pendingDelete, setPendingDelete] = useState<GroupListItem | null>(null);

  const query = useQuery({
    queryKey: ['groups', { page, pageSize, search }],
    queryFn: () => api.groups.getGroups({ page, pageSize, search: search || undefined }),
  });

  const remove = useMutation({
    mutationFn: (group: GroupListItem) => api.groups.deleteGroup(group.id),
    onSuccess: () => {
      notifications.show({ message: 'Group deleted', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['groups'] });
      setPendingDelete(null);
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const columns: Column<GroupListItem>[] = [
    {
      key: 'group',
      header: 'Group',
      render: (group) => (
        <Group gap="sm" wrap="nowrap">
          <ThemeIcon color="blue" radius="md" size={36} variant="light">
            <Icon name="users-round" size={18} />
          </ThemeIcon>
          <Box style={{ minWidth: 0 }}>
            <Text fw={600} size="sm">{group.name}</Text>
            <Text c="dimmed" size="xs" lineClamp={1}>{group.description ?? '—'}</Text>
          </Box>
        </Group>
      ),
    },
    {
      key: 'members',
      header: 'Members',
      align: 'right',
      render: (group) => <Badge color="blue" variant="light">{group.memberCount ?? 0}</Badge>,
    },
    {
      key: 'mappings',
      header: 'Mappings',
      align: 'right',
      render: (group) => <Text c="dimmed" size="sm">{group.mappingCount ?? 0}</Text>,
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (group) => (
        <RowMenu
          items={[
            { label: 'Open', icon: 'arrow-up-right', onClick: () => navigate(`/groups/${group.id}`) },
            { separator: true },
            { label: 'Delete group', icon: 'trash-2', danger: true, disabled: !canWrite, onClick: () => setPendingDelete(group) },
          ]}
        />
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Groups"
        description="Collections of users with shared role delegations and external mappings."
        actions={
          canWrite ? (
            <Button leftSection={<Icon name="plus" size={16} />} onClick={createControls.open}>
              Create group
            </Button>
          ) : undefined
        }
      />

      <FilterToolbar
        noun="groups"
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
            getRowKey={(group) => group.id}
            onRowClick={(group) => navigate(`/groups/${group.id}`)}
            isLoading={query.isLoading}
            emptyIcon="users-round"
            emptyTitle="No groups"
            emptyText="Adjust your search or create a group."
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

      {createOpened && <CreateGroupModal onClose={createControls.close} />}
      <ConfirmModal
        opened={pendingDelete !== null}
        title="Delete group"
        message={`Delete ${pendingDelete?.name ?? 'this group'}? Members keep their individual roles.`}
        confirmLabel="Delete group"
        loading={remove.isPending}
        onConfirm={() => pendingDelete && remove.mutate(pendingDelete)}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}

function CreateGroupModal({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient();
  const form = useForm({
    initialValues: { name: '', description: '' },
    validate: { name: (value) => (value.trim() ? null : 'Required') },
  });

  const mutation = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.groups.createGroup({ name: values.name, description: values.description || null }),
    onSuccess: (group) => {
      notifications.show({ message: `Created ${group.name}`, color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['groups'] });
      onClose();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Modal opened onClose={onClose} title="Create group" centered>
      <form onSubmit={form.onSubmit((values) => mutation.mutate(values))}>
        <Stack gap="md">
          <TextInput label="Group name" placeholder="Platform Engineering" required {...form.getInputProps('name')} />
          <Textarea label="Description" placeholder="Engineers who build and operate the platform." autosize minRows={2} {...form.getInputProps('description')} />
          <Group justify="flex-end" gap="sm" mt="xs">
            <Button variant="default" onClick={onClose}>Cancel</Button>
            <Button type="submit" loading={mutation.isPending}>Create group</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
