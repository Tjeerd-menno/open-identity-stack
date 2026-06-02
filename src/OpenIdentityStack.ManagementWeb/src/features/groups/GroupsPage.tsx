import {
  Alert,
  Badge,
  Button,
  Group,
  Modal,
  NativeSelect,
  Stack,
  Text,
  TextInput,
  Title,
} from '@mantine/core';
import { FormEvent, useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
import { getApiErrorMessage } from '@/lib/admin-api';
import { hasPermission } from '@/lib/permissions';
import { getRoles, type RoleListItem } from '@/features/roles/roles-api';
import { getUsers, type UserListItem } from '@/features/users/users-api';
import { GroupForm } from './GroupForm';
import {
  useAddGroupMapping,
  useAddGroupMember,
  useCreateGroup,
  useDeleteGroup,
  useGroup,
  useGroupMappings,
  useGroupMembers,
  useGroups,
  useRemoveGroupMapping,
  useRemoveGroupMember,
  useUpdateGroup,
} from './groups-hooks';
import type { AddGroupMappingRequest, GroupListItem, GroupMapping, GroupMember } from './groups-api';

const pageSize = 20;

type GroupsPageProps = {
  permissions?: string[];
};

export function GroupsPage({ permissions = ['*'] }: GroupsPageProps) {
  const location = useLocation();
  const { id } = useParams();
  const pathGroupId = getGroupIdFromPath(location.pathname);

  if (location.pathname.endsWith('/new')) {
    return <CreateGroupView permissions={permissions} />;
  }

  if (location.pathname.endsWith('/edit') && (id || pathGroupId)) {
    return <EditGroupView groupId={id ?? pathGroupId ?? ''} />;
  }

  if (id || pathGroupId) {
    return <GroupDetailView groupId={id ?? pathGroupId ?? ''} permissions={permissions} />;
  }

  return <GroupListView permissions={permissions} />;
}

function getGroupIdFromPath(pathname: string) {
  const editMatch = /^\/groups\/([^/]+)\/edit$/.exec(pathname);
  if (editMatch) {
    return editMatch[1];
  }

  return /^\/groups\/([^/]+)$/.exec(pathname)?.[1];
}

function GroupListView({ permissions }: Required<GroupsPageProps>) {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [groupToDelete, setGroupToDelete] = useState<GroupListItem | null>(null);
  const searchMounted = useRef(false);
  const groups = useGroups({ page, pageSize, search: submittedSearch || undefined });
  const deleteGroup = useDeleteGroup(groupToDelete?.id ?? '');
  const canWrite = hasPermission(permissions, 'groups:write');
  const canDelete = hasPermission(permissions, 'groups:delete');

  useEffect(() => {
    if (!searchMounted.current) {
      searchMounted.current = true;
      return;
    }

    const timer = window.setTimeout(() => {
      setSubmittedSearch(search);
      setPage(1);
    }, 300);

    return () => window.clearTimeout(timer);
  }, [search]);

  const columns: FoundationColumn<GroupListItem>[] = [
    { header: 'Name', accessorKey: 'name' },
    {
      header: 'Description',
      cell: (group) => group.description ?? '',
    },
    {
      header: 'Members',
      cell: (group) => <Badge variant="light">{group.memberCount ?? 0}</Badge>,
    },
    {
      header: 'Mappings',
      cell: (group) => <Badge variant="light">{group.mappingCount ?? 0}</Badge>,
    },
    {
      header: 'Created',
      cell: (group) => new Date(group.createdAt).toLocaleDateString(),
    },
    {
      header: 'Actions',
      cell: (group) => (
        <Group gap="xs" wrap="nowrap">
          <Button variant="subtle" size="xs" aria-label={`View ${group.name}`} onClick={() => navigate(`/groups/${group.id}`)}>
            View
          </Button>
          {canWrite && (
            <Button variant="subtle" size="xs" aria-label={`Edit ${group.name}`} onClick={() => navigate(`/groups/${group.id}/edit`)}>
              Edit
            </Button>
          )}
          {canDelete && (
            <Button variant="subtle" color="red" size="xs" aria-label={`Delete ${group.name}`} onClick={() => setGroupToDelete(group)}>
              Delete
            </Button>
          )}
        </Group>
      ),
    },
  ];

  return (
    <Stack gap="lg">
      <Group justify="space-between" align="flex-start">
        <div>
          <Title order={1}>Groups</Title>
          <Text c="dimmed">Manage group memberships and role or claim mappings.</Text>
        </div>
        {canWrite && <Button onClick={() => navigate('/groups/new')}>New group</Button>}
      </Group>

      {(!canWrite || !canDelete) && <Alert color="blue">Read-only access. Some group actions require additional permissions.</Alert>}

      <TextInput
        label="Search groups"
        maw={420}
        placeholder="Search groups by name or description..."
        value={search}
        onChange={(event) => setSearch(event.currentTarget.value)}
      />

      <FoundationTable
        columns={columns}
        data={groups.data?.items ?? []}
        isLoading={groups.isLoading}
        error={groups.isError ? getApiErrorMessage(groups.error) : null}
        emptyMessage="No groups found"
        pagination={{
          page,
          pageSize,
          totalCount: groups.data?.totalCount ?? 0,
          totalPages: groups.data?.totalPages ?? 0,
          onPageChange: setPage,
        }}
      />

      <ConfirmDialog
        opened={groupToDelete !== null}
        onOpenChange={(opened) => !opened && setGroupToDelete(null)}
        title="Delete group"
        message={`Delete ${groupToDelete?.name}? This action cannot be undone and will remove members and mappings.`}
        confirmLabel={`Delete ${groupToDelete?.name ?? 'group'}`}
        loading={deleteGroup.isPending}
        onConfirm={async () => {
          await deleteGroup.mutateAsync();
          setGroupToDelete(null);
        }}
      />
    </Stack>
  );
}

function CreateGroupView({ permissions }: Required<GroupsPageProps>) {
  const navigate = useNavigate();
  const createGroup = useCreateGroup();

  if (!hasPermission(permissions, 'groups:write')) {
    return <Alert color="blue">Read-only access. Group changes require groups:write.</Alert>;
  }

  return (
    <Stack gap="lg">
      <div>
        <Title order={1}>Create group</Title>
        <Text c="dimmed">Create a new group for membership and mappings.</Text>
      </div>
      <GroupForm
        mode="create"
        loading={createGroup.isPending}
        error={createGroup.error}
        onCancel={() => navigate('/groups')}
        onSubmit={async (data) => {
          const group = await createGroup.mutateAsync(data);
          navigate(`/groups/${group.id}`);
        }}
      />
    </Stack>
  );
}

function EditGroupView({ groupId }: { groupId: string }) {
  const navigate = useNavigate();
  const group = useGroup(groupId);
  const updateGroup = useUpdateGroup(groupId);

  if (group.isLoading) {
    return <Text>Loading group</Text>;
  }

  if (group.isError) {
    return <Alert color="red">{getApiErrorMessage(group.error)}</Alert>;
  }

  if (!group.data) {
    return <Alert color="red">Group not found.</Alert>;
  }

  return (
    <Stack gap="lg">
      <div>
        <Title order={1}>Edit group</Title>
        <Text c="dimmed">{group.data.name}</Text>
      </div>
      <GroupForm
        mode="edit"
        group={group.data}
        loading={updateGroup.isPending}
        error={updateGroup.error}
        onCancel={() => navigate(`/groups/${groupId}`)}
        onSubmit={async (data) => {
          await updateGroup.mutateAsync(data);
          navigate(`/groups/${groupId}`);
        }}
      />
    </Stack>
  );
}

function GroupDetailView({ groupId, permissions }: { groupId: string; permissions: string[] }) {
  const navigate = useNavigate();
  const group = useGroup(groupId);
  const deleteGroup = useDeleteGroup(groupId);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const canWrite = hasPermission(permissions, 'groups:write');
  const canDelete = hasPermission(permissions, 'groups:delete');
  const canManageMembers = hasPermission(permissions, 'groups:manage-members');

  if (group.isLoading) {
    return <Text>Loading group</Text>;
  }

  if (group.isError) {
    return <Alert color="red">{getApiErrorMessage(group.error)}</Alert>;
  }

  if (!group.data) {
    return <Alert color="red">Group not found.</Alert>;
  }

  return (
    <Stack gap="lg" role="region" aria-label="Group details">
      <Group justify="space-between" align="flex-start">
        <div>
          <Title order={1}>{group.data.name}</Title>
          {group.data.description && <Text c="dimmed">{group.data.description}</Text>}
        </div>
        <Group>
          {canWrite && <Button onClick={() => navigate(`/groups/${groupId}/edit`)}>Edit group</Button>}
          {canDelete && (
            <Button color="red" variant="light" onClick={() => setConfirmDelete(true)}>
              Delete group
            </Button>
          )}
        </Group>
      </Group>

      <section aria-label="Group metadata">
        <Title order={2} size="h3">Group information</Title>
        <Stack gap={4} mt="sm">
          <Text size="sm">Group ID: {group.data.id}</Text>
          <Text size="sm">Members: {group.data.memberCount ?? 0}</Text>
          <Text size="sm">Mappings: {group.data.mappingCount ?? 0}</Text>
          <Text size="sm">Created: {new Date(group.data.createdAt).toLocaleString()}</Text>
        </Stack>
      </section>

      <GroupMembersSection groupId={groupId} canManageMembers={canManageMembers} />
      <GroupMappingsSection groupId={groupId} canWrite={canWrite} />

      <ConfirmDialog
        opened={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Delete group"
        message={`Delete ${group.data.name}? This action cannot be undone and will remove members and mappings.`}
        confirmLabel={`Delete ${group.data.name}`}
        loading={deleteGroup.isPending}
        onConfirm={async () => {
          await deleteGroup.mutateAsync();
          navigate('/groups');
        }}
      />
    </Stack>
  );
}

function GroupMembersSection({ groupId, canManageMembers }: { groupId: string; canManageMembers: boolean }) {
  const members = useGroupMembers(groupId);
  const addMember = useAddGroupMember(groupId);
  const removeMember = useRemoveGroupMember(groupId);
  const [addOpen, setAddOpen] = useState(false);
  const [memberToRemove, setMemberToRemove] = useState<GroupMember | null>(null);

  return (
    <section aria-label="Group members">
      <Group justify="space-between" align="center">
        <Title order={2} size="h3">Members ({members.data?.items.length ?? 0})</Title>
        {canManageMembers && <Button size="sm" onClick={() => setAddOpen(true)}>Add member</Button>}
      </Group>

      {members.isLoading && <Text c="dimmed">Loading members</Text>}
      {members.isError && <Alert color="red">{getApiErrorMessage(members.error)}</Alert>}
      {!members.isLoading && (members.data?.items.length ?? 0) === 0 && <Text c="dimmed">No members in this group</Text>}
      <Stack gap="xs" mt="sm">
        {(members.data?.items ?? []).map((member) => (
          <Group key={member.userId} justify="space-between">
            <div>
              <Text fw={500}>{member.displayName}</Text>
              <Text size="sm" c="dimmed">{member.email}</Text>
            </div>
            {canManageMembers && (
              <Button
                size="xs"
                variant="subtle"
                color="red"
                aria-label={`Remove member ${member.displayName}`}
                onClick={() => setMemberToRemove(member)}
              >
                Remove
              </Button>
            )}
          </Group>
        ))}
      </Stack>

      <AddMemberDialog
        opened={addOpen}
        loading={addMember.isPending}
        onOpenChange={setAddOpen}
        onSubmit={async (userId) => {
          await addMember.mutateAsync(userId);
          setAddOpen(false);
        }}
      />

      <ConfirmDialog
        opened={memberToRemove !== null}
        onOpenChange={(opened) => !opened && setMemberToRemove(null)}
        title="Remove member"
        message={`Remove ${memberToRemove?.displayName ?? 'this member'} from the group?`}
        confirmLabel="Remove member"
        loading={removeMember.isPending}
        onConfirm={async () => {
          await removeMember.mutateAsync(memberToRemove?.userId ?? '');
          setMemberToRemove(null);
        }}
      />
    </section>
  );
}

function GroupMappingsSection({ groupId, canWrite }: { groupId: string; canWrite: boolean }) {
  const mappings = useGroupMappings(groupId);
  const addMapping = useAddGroupMapping(groupId);
  const removeMapping = useRemoveGroupMapping(groupId);
  const [addOpen, setAddOpen] = useState(false);
  const [mappingToRemove, setMappingToRemove] = useState<GroupMapping | null>(null);

  return (
    <section aria-label="Group mappings">
      <Group justify="space-between" align="center">
        <Title order={2} size="h3">Mappings ({mappings.data?.items.length ?? 0})</Title>
        {canWrite && <Button size="sm" onClick={() => setAddOpen(true)}>Add mapping</Button>}
      </Group>

      {mappings.isLoading && <Text c="dimmed">Loading mappings</Text>}
      {mappings.isError && <Alert color="red">{getApiErrorMessage(mappings.error)}</Alert>}
      {!mappings.isLoading && (mappings.data?.items.length ?? 0) === 0 && <Text c="dimmed">No mappings configured for this group</Text>}
      <Stack gap="xs" mt="sm">
        {(mappings.data?.items ?? []).map((mapping) => (
          <Group key={mapping.id} justify="space-between">
            <Group gap="xs">
              <Badge variant="light">{mapping.type}</Badge>
              <Text ff="monospace" size="sm">{mapping.value}</Text>
            </Group>
            {canWrite && (
              <Button
                size="xs"
                variant="subtle"
                color="red"
                aria-label={`Remove mapping ${mapping.value}`}
                onClick={() => setMappingToRemove(mapping)}
              >
                Remove
              </Button>
            )}
          </Group>
        ))}
      </Stack>

      <AddMappingDialog
        opened={addOpen}
        loading={addMapping.isPending}
        onOpenChange={setAddOpen}
        onSubmit={async (data) => {
          await addMapping.mutateAsync(data);
          setAddOpen(false);
        }}
      />

      <ConfirmDialog
        opened={mappingToRemove !== null}
        onOpenChange={(opened) => !opened && setMappingToRemove(null)}
        title="Remove mapping"
        message={`Remove mapping ${mappingToRemove?.value ?? ''}?`}
        confirmLabel="Remove mapping"
        loading={removeMapping.isPending}
        onConfirm={async () => {
          await removeMapping.mutateAsync(mappingToRemove?.id ?? '');
          setMappingToRemove(null);
        }}
      />
    </section>
  );
}

function AddMemberDialog({
  opened,
  loading,
  onOpenChange,
  onSubmit,
}: {
  opened: boolean;
  loading: boolean;
  onOpenChange: (opened: boolean) => void;
  onSubmit: (userId: string) => Promise<void>;
}) {
  const [selectedUserId, setSelectedUserId] = useState('');
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!opened) {
      return;
    }

    let cancelled = false;
    getUsers({ page: 1, pageSize: 100 })
      .then((response) => {
        if (!cancelled) {
          setUsers(response.items);
        }
      })
      .catch((unknownError) => {
        if (!cancelled) {
          setError(getApiErrorMessage(unknownError));
        }
      });

    return () => {
      cancelled = true;
    };
  }, [opened]);

  return (
    <Modal opened={opened} onClose={() => onOpenChange(false)} title="Add member" centered>
      <Stack gap="md">
        {error && <Alert color="red">{error}</Alert>}
        <NativeSelect
          label="Select user"
          value={selectedUserId}
          onChange={(event) => setSelectedUserId(event.currentTarget.value)}
          data={[
            { value: '', label: 'Select a user...' },
            ...users.map((user) => ({ value: user.id, label: `${user.displayName} (${user.email})` })),
          ]}
        />
        <Group justify="flex-end">
          <Button variant="default" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button disabled={!selectedUserId} loading={loading} onClick={() => void onSubmit(selectedUserId)}>
            Add member
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}

function AddMappingDialog({
  opened,
  loading,
  onOpenChange,
  onSubmit,
}: {
  opened: boolean;
  loading: boolean;
  onOpenChange: (opened: boolean) => void;
  onSubmit: (data: AddGroupMappingRequest) => Promise<void>;
}) {
  const [type, setType] = useState<'Role' | 'Claim' | ''>('');
  const [value, setValue] = useState('');
  const [roles, setRoles] = useState<RoleListItem[]>([]);
  const [rolesLoading, setRolesLoading] = useState(false);
  const [rolesError, setRolesError] = useState<string | null>(null);

  useEffect(() => {
    if (!opened || type !== 'Role') {
      return;
    }

    let cancelled = false;
    setRolesLoading(true);
    setRolesError(null);

    getRoles({ page: 1, pageSize: 100 })
      .then((response) => {
        if (!cancelled) {
          setRoles(response.items);
        }
      })
      .catch((unknownError) => {
        if (!cancelled) {
          setRolesError(getApiErrorMessage(unknownError));
        }
      })
      .finally(() => {
        if (!cancelled) {
          setRolesLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [opened, type]);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!type || !value.trim()) {
      return;
    }

    void onSubmit({ type, value: value.trim() });
  }

  return (
    <Modal opened={opened} onClose={() => onOpenChange(false)} title="Add mapping" centered>
      <form onSubmit={handleSubmit}>
        <Stack gap="md">
          <NativeSelect
            label="Mapping type"
            value={type}
            onChange={(event) => {
              setType(event.currentTarget.value as 'Role' | 'Claim' | '');
              setValue('');
            }}
            data={[
              { value: '', label: 'Select type...' },
              { value: 'Role', label: 'Role' },
              { value: 'Claim', label: 'Claim' },
            ]}
          />
          {type === 'Role' ? (
            <>
              {rolesError && <Alert color="red">{rolesError}</Alert>}
              <NativeSelect
                label="Select role"
                value={value}
                onChange={(event) => setValue(event.currentTarget.value)}
                data={[
                  { value: '', label: rolesLoading ? 'Loading roles...' : 'Select a role...' },
                  ...roles.map((role) => ({ value: role.id, label: role.displayName })),
                ]}
              />
            </>
          ) : (
            <TextInput
              label="Mapping value"
              value={value}
              onChange={(event) => setValue(event.currentTarget.value)}
              placeholder="department:engineering"
            />
          )}
          <Group justify="flex-end">
            <Button type="button" variant="default" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" disabled={!type || !value.trim()} loading={loading}>
              Add mapping
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
