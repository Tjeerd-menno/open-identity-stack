import {
  Avatar,
  Badge,
  Box,
  Button,
  Group,
  Modal,
  Select,
  Stack,
  Tabs,
  Text,
  TextInput,
  Textarea,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import type { Group as GroupModel, GroupMapping, GroupMember } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { DataTable, type Column } from '@/components/DataTable';
import { SearchInput } from '@/components/ListControls';
import { RowMenu } from '@/components/RowMenu';
import { ConfirmModal } from '@/components/ConfirmModal';
import { BackLink, CenteredState, DetailHeader, ErrorState, MetaStrip, SectionCard } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import { formatDateTime, getInitials } from '@/lib/format';
import { useSyncedForm } from '@/lib/use-synced-form';

export function GroupDetailPage() {
  const { groupId = '' } = useParams();
  const navigate = useNavigate();
  const auth = useAuth();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(auth.permissions, 'groups:write');
  // Membership endpoints require groups:manage-members; deletion requires groups:delete —
  // both are authorized independently of groups:write.
  const canManageMembers = hasPermission(auth.permissions, 'groups:manage-members');
  const canDelete = hasPermission(auth.permissions, 'groups:delete');
  const canReadRoles = hasPermission(auth.permissions, 'roles:read');
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const [addMemberOpened, addMemberControls] = useDisclosure(false);
  const [addMappingOpened, addMappingControls] = useDisclosure(false);

  const groupQuery = useQuery({ queryKey: ['group', groupId], queryFn: () => api.groups.getGroup(groupId) });
  const membersQuery = useQuery({
    queryKey: ['group', groupId, 'members', { page, pageSize }],
    queryFn: () => api.groups.getGroupMembers(groupId, { page, pageSize }),
  });
  const mappingsQuery = useQuery({
    queryKey: ['group', groupId, 'mappings'],
    queryFn: () => api.groups.getGroupMappings(groupId),
  });

  const invalidateMembers = () => {
    void queryClient.invalidateQueries({ queryKey: ['group', groupId, 'members'] });
    void queryClient.invalidateQueries({ queryKey: ['group', groupId] });
  };
  const invalidateMappings = () => {
    void queryClient.invalidateQueries({ queryKey: ['group', groupId, 'mappings'] });
    void queryClient.invalidateQueries({ queryKey: ['group', groupId] });
  };

  const removeMember = useMutation({
    mutationFn: (member: GroupMember) => api.groups.removeMemberFromGroup(groupId, member.userId),
    onSuccess: () => {
      notifications.show({ message: 'Member removed', color: 'green' });
      invalidateMembers();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const removeMapping = useMutation({
    mutationFn: (mapping: GroupMapping) => api.groups.removeGroupMapping(groupId, mapping.id),
    onSuccess: () => {
      notifications.show({ message: 'Mapping removed', color: 'green' });
      invalidateMappings();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  if (groupQuery.isLoading) {
    return <CenteredState loading title="Loading group…" />;
  }
  if (groupQuery.isError || !groupQuery.data) {
    return <ErrorState message={getApiErrorMessage(groupQuery.error)} />;
  }

  const group = groupQuery.data;
  const mappings = mappingsQuery.data?.items ?? [];
  // The group endpoint does not return memberCount, so it is always undefined.
  // Do not fall back to the current page length — that would misrepresent a paginated
  // subset as the total. Infer whether a next page exists from whether a full page returned.
  const memberCount = group.memberCount;
  const hasNextPage = (membersQuery.data?.items.length ?? 0) === pageSize;

  const memberColumns: Column<GroupMember>[] = [
    {
      key: 'member',
      header: 'User',
      render: (member) => (
        <Group gap="sm" wrap="nowrap">
          <Avatar color="blue" name={member.displayName} size={34} radius="xl">
            {getInitials(member.displayName)}
          </Avatar>
          <Box style={{ minWidth: 0 }}>
            <Text fw={600} size="sm" truncate>
              {member.displayName}
            </Text>
            <Text c="dimmed" size="xs" truncate>
              {member.email}
            </Text>
          </Box>
        </Group>
      ),
    },
    {
      key: 'added',
      header: 'Added',
      render: (member) => (
        <Text c="dimmed" size="sm">
          {member.addedAt ? formatDateTime(member.addedAt) : '—'}
        </Text>
      ),
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (member) => (
        <RowMenu
          items={[
            { label: 'Open user', icon: 'arrow-up-right', onClick: () => navigate(`/users/${member.userId}`) },
            { separator: true },
            { label: 'Remove from group', icon: 'user-minus', danger: true, disabled: !canManageMembers, onClick: () => removeMember.mutate(member) },
          ]}
        />
      ),
    },
  ];

  return (
    <div>
      <BackLink label="Back to groups" to="/groups" />
      <DetailHeader
        icon="users-round"
        title={group.name}
        description={group.description ?? undefined}
        actions={
          canManageMembers ? (
            <Button variant="default" leftSection={<Icon name="user-plus" size={16} />} onClick={addMemberControls.open}>
              Add members
            </Button>
          ) : undefined
        }
      />

      <MetaStrip
        items={[
          { label: 'Members', value: memberCount ?? '—' },
          { label: 'Mappings', value: group.mappingCount ?? mappings.length },
          { label: 'Created', value: formatDateTime(group.createdAt) },
        ]}
      />

      <Tabs defaultValue="members" color="blue" keepMounted={false}>
        <Tabs.List mb="lg">
          <Tabs.Tab value="members">Members{memberCount !== undefined ? ` (${memberCount})` : ''}</Tabs.Tab>
          <Tabs.Tab value="mappings">Mappings ({mappings.length})</Tabs.Tab>
          <Tabs.Tab value="settings">Settings</Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="members">
          <DataTable
            columns={memberColumns}
            rows={membersQuery.data?.items ?? []}
            getRowKey={(member) => member.userId}
            isLoading={membersQuery.isLoading}
            emptyIcon="users"
            emptyTitle="No members"
            emptyText="Add users to this group to grant them its delegated access."
          />
          {(page > 1 || hasNextPage) && (
            <Group justify="space-between" mt="md">
              <Text c="dimmed" size="sm">
                Page {page}
              </Text>
              <Group gap="sm">
                <Button
                  variant="default"
                  size="xs"
                  leftSection={<Icon name="arrow-left" size={14} />}
                  disabled={page <= 1 || membersQuery.isFetching}
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="default"
                  size="xs"
                  rightSection={<Icon name="arrow-right" size={14} />}
                  disabled={!hasNextPage || membersQuery.isFetching}
                  onClick={() => setPage((current) => current + 1)}
                >
                  Next
                </Button>
              </Group>
            </Group>
          )}
        </Tabs.Panel>

        <Tabs.Panel value="mappings">
          <SectionCard
            title="External mappings"
            description="Role and claim mappings that grant access based on upstream group membership."
            right={
              canWrite ? (
                <Button size="xs" leftSection={<Icon name="plus" size={14} />} onClick={addMappingControls.open}>
                  Add mapping
                </Button>
              ) : undefined
            }
          >
            {mappings.length === 0 ? (
              <Text c="dimmed" size="sm">
                No mappings configured.
              </Text>
            ) : (
              <Stack gap="xs">
                {mappings.map((mapping) => (
                  <Group key={mapping.id} justify="space-between" wrap="nowrap" gap="md">
                    <Group gap="sm" wrap="nowrap">
                      <Badge color={mapping.type === 'Role' ? 'blue' : 'grape'} variant="light">
                        {mapping.type}
                      </Badge>
                      <Text className="mw-mono" size="sm">
                        {mapping.value}
                      </Text>
                    </Group>
                    {canWrite && (
                      <Button color="red" size="xs" variant="subtle" onClick={() => removeMapping.mutate(mapping)}>
                        Remove
                      </Button>
                    )}
                  </Group>
                ))}
              </Stack>
            )}
          </SectionCard>
        </Tabs.Panel>

        <Tabs.Panel value="settings">
          <GroupSettings group={group} canWrite={canWrite} canDelete={canDelete} onDeleted={() => navigate('/groups')} />
        </Tabs.Panel>
      </Tabs>

      {addMemberOpened && (
        <AddMembersModal groupId={groupId} onAdded={invalidateMembers} onClose={addMemberControls.close} />
      )}
      {addMappingOpened && (
        <AddMappingModal
          groupId={groupId}
          canReadRoles={canReadRoles}
          onAdded={invalidateMappings}
          onClose={addMappingControls.close}
        />
      )}
    </div>
  );
}

function AddMembersModal({ groupId, onAdded, onClose }: { groupId: string; onAdded: () => void; onClose: () => void }) {
  const [search, setSearch] = useState('');
  const usersQuery = useQuery({
    queryKey: ['users', 'picker', search],
    queryFn: () => api.users.getUsers({ page: 1, pageSize: 8, search: search || undefined }),
  });

  const add = useMutation({
    mutationFn: (userId: string) => api.groups.addMemberToGroup(groupId, userId),
    onSuccess: () => {
      notifications.show({ message: 'Member added', color: 'green' });
      onAdded();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Modal opened onClose={onClose} title="Add members" centered>
      <Stack gap="md">
        <SearchInput placeholder="Search users…" value={search} onChange={setSearch} width="100%" />
        <Stack gap={4}>
          {(usersQuery.data?.items ?? []).map((user) => (
            <Group
              key={user.id}
              justify="space-between"
              wrap="nowrap"
              gap="sm"
              p="xs"
              style={{ borderRadius: 'var(--mantine-radius-md)' }}
            >
              <Group gap="sm" wrap="nowrap">
                <Avatar color="blue" name={user.displayName} size={32} radius="xl">
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
              <Button
                size="xs"
                variant="light"
                leftSection={<Icon name="plus" size={14} />}
                loading={add.isPending && add.variables === user.id}
                onClick={() => add.mutate(user.id)}
              >
                Add
              </Button>
            </Group>
          ))}
          {(usersQuery.data?.items.length ?? 0) === 0 && (
            <Text c="dimmed" py="sm" size="sm" ta="center">
              No users match your search.
            </Text>
          )}
        </Stack>
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Done
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}

function AddMappingModal({
  groupId,
  canReadRoles,
  onAdded,
  onClose,
}: {
  groupId: string;
  canReadRoles: boolean;
  onAdded: () => void;
  onClose: () => void;
}) {
  const form = useForm({
    initialValues: { type: 'Role' as 'Role' | 'Claim', value: '' },
    validate: { value: (value) => (value.trim() ? null : 'Required') },
  });

  // Role mappings are persisted by role ID (AddGroupMapping treats the value as the target
  // role identifier), so offer a role picker rather than a free-text name field.
  const rolesQuery = useQuery({
    queryKey: ['roles', 'all'],
    queryFn: () => api.roles.getRoles({ page: 1, pageSize: 100 }),
    enabled: canReadRoles,
  });

  const mutation = useMutation({
    mutationFn: (values: typeof form.values) => api.groups.addGroupMapping(groupId, { type: values.type, value: values.value }),
    onSuccess: () => {
      notifications.show({ message: 'Mapping added', color: 'green' });
      onAdded();
      onClose();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Modal opened onClose={onClose} title="Add mapping" centered>
      <form onSubmit={form.onSubmit((values) => mutation.mutate(values))}>
        <Stack gap="md">
          <Select
            label="Type"
            data={['Role', 'Claim']}
            allowDeselect={false}
            value={form.values.type}
            onChange={(value) => {
              if (!value) return;
              form.setFieldValue('type', value as 'Role' | 'Claim');
              form.setFieldValue('value', '');
            }}
          />
          {form.values.type === 'Role' ? (
            <Select
              label="Role"
              description="The platform role granted to members of this group."
              placeholder={canReadRoles ? 'Select a role' : 'Requires roles:read'}
              searchable
              required
              disabled={!canReadRoles}
              data={(rolesQuery.data?.items ?? []).map((role) => ({ value: role.id, label: role.displayName }))}
              value={form.values.value || null}
              error={form.errors.value}
              onChange={(value) => form.setFieldValue('value', value ?? '')}
            />
          ) : (
            <TextInput
              label="Claim"
              description="The upstream claim to match, as claimType or claimType:claimValue."
              placeholder="group:engineering"
              required
              {...form.getInputProps('value')}
            />
          )}
          <Group justify="flex-end" gap="sm" mt="xs">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={mutation.isPending}>
              Add mapping
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

function GroupSettings({ group, canWrite, canDelete, onDeleted }: { group: GroupModel; canWrite: boolean; canDelete: boolean; onDeleted: () => void }) {
  const queryClient = useQueryClient();
  const [confirmDeleteOpened, confirmDeleteControls] = useDisclosure(false);
  const form = useForm({
    initialValues: { name: group.name, description: group.description ?? '' },
    validate: { name: (value) => (value.trim() ? null : 'Required') },
  });

  useSyncedForm(form, { name: group.name, description: group.description ?? '' });

  const save = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.groups.updateGroup(group.id, { name: values.name, description: values.description || null }),
    onSuccess: () => {
      notifications.show({ message: 'Group updated', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['group', group.id] });
      void queryClient.invalidateQueries({ queryKey: ['groups'] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const remove = useMutation({
    mutationFn: () => api.groups.deleteGroup(group.id),
    onSuccess: () => {
      notifications.show({ message: 'Group deleted', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['groups'] });
      onDeleted();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Stack gap="lg">
      <SectionCard title="General">
        <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
          <Stack gap="md">
            <TextInput label="Group name" disabled={!canWrite} {...form.getInputProps('name')} />
            <Textarea label="Description" autosize minRows={2} disabled={!canWrite} {...form.getInputProps('description')} />
            {canWrite && (
              <Group justify="flex-end">
                <Button type="submit" loading={save.isPending} disabled={!form.isDirty()}>
                  Save changes
                </Button>
              </Group>
            )}
          </Stack>
        </form>
      </SectionCard>

      {canDelete && (
        <SectionCard title="Danger zone" description="Deleting a group removes its delegated access. Members keep their individual roles." danger>
          <Button color="red" variant="light" leftSection={<Icon name="trash-2" size={16} />} onClick={confirmDeleteControls.open}>
            Delete group
          </Button>
        </SectionCard>
      )}

      <ConfirmModal
        opened={confirmDeleteOpened}
        title="Delete group"
        message={`Permanently delete ${group.name}? This cannot be undone.`}
        confirmLabel="Delete group"
        loading={remove.isPending}
        onConfirm={() => remove.mutate()}
        onClose={confirmDeleteControls.close}
      />
    </Stack>
  );
}
