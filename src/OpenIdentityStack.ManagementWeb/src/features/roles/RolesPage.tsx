import { Alert, Badge, Button, Group, Stack, Text, Title } from '@mantine/core';
import { useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { EntityActionGroup } from '@/components/EntityActionMenu';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
import { PageHeader, PageToolbar } from '@/components/PagePrimitives';
import { getApiErrorMessage } from '@/lib/admin-api';
import { hasPermission } from '@/lib/permissions';
import { RoleForm } from './RoleForm';
import { RoleStatusBadge, RoleTypeBadge } from './RoleBadges';
import {
  useCreateRole,
  useDeleteRole,
  useRole,
  useRoles,
  useUpdateRole,
} from './roles-hooks';
import type { CreateRoleRequest, RoleListItem, UpdateRoleRequest } from './roles-api';

const pageSize = 20;

type RolesPageProps = {
  permissions?: string[];
};

export function RolesPage({ permissions = ['*'] }: RolesPageProps) {
  const location = useLocation();
  const { id } = useParams();
  const pathRoleId = getRoleIdFromPath(location.pathname);

  if (location.pathname.endsWith('/new')) {
    return <CreateRoleView permissions={permissions} />;
  }

  if (id || pathRoleId) {
    return <RoleDetailView roleId={id ?? pathRoleId ?? ''} permissions={permissions} />;
  }

  return <RoleListView permissions={permissions} />;
}

function getRoleIdFromPath(pathname: string) {
  const match = /^\/roles\/([^/]+)$/.exec(pathname);
  return match?.[1];
}

function RoleListView({ permissions }: Required<RolesPageProps>) {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const searchMounted = useRef(false);
  const roles = useRoles({ page, pageSize, search: submittedSearch || undefined });
  const canWrite = hasPermission(permissions, 'roles:write');

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

  const columns: FoundationColumn<RoleListItem>[] = [
    { header: 'Name', accessorKey: 'name' },
    { header: 'Display Name', accessorKey: 'displayName' },
    {
      header: 'Type',
      cell: (role) => <RoleTypeBadge isSystemRole={role.isSystemRole} />,
    },
    {
      header: 'Status',
      cell: (role) => <RoleStatusBadge isActive={role.isActive} />,
    },
    {
      header: 'Permissions',
      cell: (role) => <Badge variant="light">{role.permissions?.length ?? 0}</Badge>,
    },
    {
      header: 'Actions',
      align: 'right',
      cell: (role) => (
        <EntityActionGroup
          actions={[
            { label: `View ${role.displayName}`, onClick: () => navigate(`/roles/${role.id}`) },
          ]}
        />
      ),
    },
  ];

  return (
    <Stack gap="lg">
      <PageHeader
        title="Roles"
        description="Manage platform role grants and permission assignments."
        actions={canWrite && <Button onClick={() => navigate('/roles/new')}>New role</Button>}
      />

      {!canWrite && <Alert color="blue">Read-only access. Role changes require roles:write.</Alert>}

      <PageToolbar
        searchLabel="Search roles"
        searchPlaceholder="Search by name or display name..."
        searchValue={search}
        resultCount={roles.data?.totalCount}
        onSearchChange={setSearch}
        onClear={() => {
          setSearch('');
          setSubmittedSearch('');
          setPage(1);
        }}
      />

      <FoundationTable
        columns={columns}
        data={roles.data?.items ?? []}
        isLoading={roles.isLoading}
        error={roles.isError ? getApiErrorMessage(roles.error) : null}
        emptyMessage="No roles found"
        pagination={{
          page,
          pageSize,
          totalCount: roles.data?.totalCount ?? 0,
          totalPages: roles.data?.totalPages ?? 0,
          onPageChange: setPage,
        }}
      />
    </Stack>
  );
}

function CreateRoleView({ permissions }: Required<RolesPageProps>) {
  const navigate = useNavigate();
  const createRole = useCreateRole();
  const canWrite = hasPermission(permissions, 'roles:write');

  if (!canWrite) {
    return <Alert color="blue">Read-only access. Role changes require roles:write.</Alert>;
  }

  return (
    <Stack gap="lg">
      <div>
        <Title order={1}>Create role</Title>
        <Text c="dimmed">Create a custom platform role.</Text>
      </div>
      <RoleForm
        mode="create"
        loading={createRole.isPending}
        error={createRole.error}
        onCancel={() => navigate('/roles')}
        onSubmit={async (data) => {
          const role = await createRole.mutateAsync(data as CreateRoleRequest);
          navigate(`/roles/${role.id}`);
        }}
      />
    </Stack>
  );
}

function RoleDetailView({ roleId, permissions }: { roleId: string; permissions: string[] }) {
  const navigate = useNavigate();
  const role = useRole(roleId);
  const updateRole = useUpdateRole(roleId);
  const deleteRole = useDeleteRole(roleId);
  const [isEditing, setIsEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const canWrite = hasPermission(permissions, 'roles:write');

  if (role.isLoading) {
    return <Text>Loading role</Text>;
  }

  if (role.isError) {
    return <Alert color="red">{getApiErrorMessage(role.error)}</Alert>;
  }

  if (!role.data) {
    return <Alert color="red">Role not found.</Alert>;
  }

  if (isEditing) {
    return (
      <Stack gap="lg">
        <div>
          <Title order={1}>Edit role</Title>
          <Text c="dimmed">{role.data.displayName}</Text>
        </div>
        <RoleForm
          mode="edit"
          role={role.data}
          loading={updateRole.isPending}
          error={updateRole.error}
          onCancel={() => setIsEditing(false)}
          onSubmit={async (data) => {
            await updateRole.mutateAsync(data as UpdateRoleRequest);
            setIsEditing(false);
          }}
        />
      </Stack>
    );
  }

  return (
    <Stack gap="lg" role="region" aria-label="Role details">
      <Group justify="space-between" align="flex-start">
        <div>
          <Title order={1}>{role.data.displayName}</Title>
          <Text c="dimmed">{role.data.name}</Text>
        </div>
        <Group>
          <RoleTypeBadge isSystemRole={role.data.isSystemRole} />
          <RoleStatusBadge isActive={role.data.isActive} />
        </Group>
      </Group>

      {role.data.isSystemRole && (
        <Alert color="blue">
          This is a system role and cannot be deleted. Permissions can be modified but the role will always exist in the system.
        </Alert>
      )}

      {!canWrite && <Alert color="blue">Read-only access. Role changes require roles:write.</Alert>}

      {role.data.description && (
        <section aria-label="Role description">
          <Title order={2} size="h3">Description</Title>
          <Text>{role.data.description}</Text>
        </section>
      )}

      <section aria-label="Role permissions">
        <Title order={2} size="h3">Permissions</Title>
        <Group mt="sm" gap="xs">
          {role.data.permissions.map((permission) => (
            <Badge key={permission} variant="light">{permission}</Badge>
          ))}
        </Group>
      </section>

      <section aria-label="Role metadata">
        <Title order={2} size="h3">Metadata</Title>
        <Stack gap={4} mt="sm">
          <Text size="sm">Role ID: {role.data.id}</Text>
          <Text size="sm">Permission count: {role.data.permissions.length}</Text>
        </Stack>
      </section>

      {canWrite && (
        <Group>
          <Button onClick={() => setIsEditing(true)}>Edit role</Button>
          {!role.data.isSystemRole && (
            <Button color="red" variant="light" onClick={() => setConfirmDelete(true)}>
              Delete role
            </Button>
          )}
        </Group>
      )}

      <ConfirmDialog
        opened={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Delete role"
        message={`Delete ${role.data.displayName}? This action cannot be undone.`}
        confirmLabel={`Delete ${role.data.displayName}`}
        loading={deleteRole.isPending}
        onConfirm={async () => {
          await deleteRole.mutateAsync();
          navigate('/roles');
        }}
      />
    </Stack>
  );
}
