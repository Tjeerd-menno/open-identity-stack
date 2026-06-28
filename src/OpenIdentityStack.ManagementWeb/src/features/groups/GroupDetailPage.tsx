import { Avatar, Badge, Box, Button, Group, Stack, Tabs, Text, TextInput, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import type { Group as GroupModel, GroupMapping, GroupMember } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { DataTable, type Column } from '@/components/DataTable';
import { Pager } from '@/components/ListControls';
import { RowMenu } from '@/components/RowMenu';
import { BackLink, CenteredState, DetailHeader, ErrorState, MetaStrip, SectionCard } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import { formatDateTime, getInitials } from '@/lib/format';

export function GroupDetailPage() {
  const { groupId = '' } = useParams();
  const navigate = useNavigate();
  const auth = useAuth();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(auth.permissions, 'groups:write');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  const groupQuery = useQuery({ queryKey: ['group', groupId], queryFn: () => api.groups.getGroup(groupId) });
  const membersQuery = useQuery({
    queryKey: ['group', groupId, 'members', { page, pageSize }],
    queryFn: () => api.groups.getGroupMembers(groupId, { page, pageSize }),
  });
  const mappingsQuery = useQuery({
    queryKey: ['group', groupId, 'mappings'],
    queryFn: () => api.groups.getGroupMappings(groupId),
  });

  const removeMember = useMutation({
    mutationFn: (member: GroupMember) => api.groups.removeMemberFromGroup(groupId, member.userId),
    onSuccess: () => {
      notifications.show({ message: 'Member removed', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['group', groupId, 'members'] });
      void queryClient.invalidateQueries({ queryKey: ['group', groupId] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const removeMapping = useMutation({
    mutationFn: (mapping: GroupMapping) => api.groups.removeGroupMapping(groupId, mapping.id),
    onSuccess: () => {
      notifications.show({ message: 'Mapping removed', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['group', groupId, 'mappings'] });
      void queryClient.invalidateQueries({ queryKey: ['group', groupId] });
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
            { label: 'Remove from group', icon: 'user-minus', danger: true, disabled: !canWrite, onClick: () => removeMember.mutate(member) },
          ]}
        />
      ),
    },
  ];

  return (
    <div>
      <BackLink label="Back to groups" to="/groups" />
      <DetailHeader icon="users-round" title={group.name} description={group.description ?? undefined} />

      <MetaStrip
        items={[
          { label: 'Members', value: group.memberCount ?? membersQuery.data?.totalCount ?? 0 },
          { label: 'Mappings', value: group.mappingCount ?? mappings.length },
          { label: 'Created', value: formatDateTime(group.createdAt) },
        ]}
      />

      <Tabs defaultValue="members" color="blue" keepMounted={false}>
        <Tabs.List mb="lg">
          <Tabs.Tab value="members">Members ({membersQuery.data?.totalCount ?? group.memberCount ?? 0})</Tabs.Tab>
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
            emptyText="This group has no members yet."
          />
          <Pager
            page={page}
            pageSize={pageSize}
            totalCount={membersQuery.data?.totalCount ?? 0}
            totalPages={membersQuery.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setPage(1);
            }}
          />
        </Tabs.Panel>

        <Tabs.Panel value="mappings">
          <SectionCard title="External mappings" description="Role and claim mappings that grant access based on upstream group membership.">
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
                      <Button
                        color="red"
                        size="xs"
                        variant="subtle"
                        onClick={() => removeMapping.mutate(mapping)}
                      >
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
          <GroupSettings group={group} canWrite={canWrite} onDeleted={() => navigate('/groups')} />
        </Tabs.Panel>
      </Tabs>
    </div>
  );
}

function GroupSettings({ group, canWrite, onDeleted }: { group: GroupModel; canWrite: boolean; onDeleted: () => void }) {
  const queryClient = useQueryClient();
  const form = useForm({
    initialValues: { name: group.name, description: group.description ?? '' },
    validate: { name: (value) => (value.trim() ? null : 'Required') },
  });

  useEffect(() => {
    form.setValues({ name: group.name, description: group.description ?? '' });
    form.resetDirty({ name: group.name, description: group.description ?? '' });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [group.id]);

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

      {canWrite && (
        <SectionCard title="Danger zone" description="Deleting a group removes its delegated access. Members keep their individual roles." danger>
          <Button color="red" variant="light" leftSection={<Icon name="trash-2" size={16} />} loading={remove.isPending} onClick={() => remove.mutate()}>
            Delete group
          </Button>
        </SectionCard>
      )}
    </Stack>
  );
}
