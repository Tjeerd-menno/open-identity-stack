import { ActionIcon, Badge, Button, Group, Select, Stack, Tabs, Text, TextInput, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import type { Role } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { ConfirmModal } from '@/components/ConfirmModal';
import { BackLink, CenteredState, DetailHeader, ErrorState, MetaStrip, SectionCard } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';

type PermissionEntry = { action: string; key: string };

/** Group permission strings by their resource family, keeping the original key. */
function groupPermissions(permissions: string[]): Array<{ family: string; items: PermissionEntry[] }> {
  const byFamily = new Map<string, PermissionEntry[]>();
  for (const permission of permissions) {
    const [family, action = '*'] = permission.split(':');
    byFamily.set(family, [...(byFamily.get(family) ?? []), { action, key: permission }]);
  }
  return [...byFamily.entries()].map(([family, items]) => ({ family, items }));
}

export function RoleDetailPage() {
  const { roleId = '' } = useParams();
  const navigate = useNavigate();
  const auth = useAuth();
  const canWrite = hasPermission(auth.permissions, 'roles:write');

  const roleQuery = useQuery({ queryKey: ['role', roleId], queryFn: () => api.roles.getRole(roleId) });

  if (roleQuery.isLoading) {
    return <CenteredState loading title="Loading role…" />;
  }
  if (roleQuery.isError || !roleQuery.data) {
    return <ErrorState message={getApiErrorMessage(roleQuery.error)} />;
  }

  const role = roleQuery.data;

  return (
    <div>
      <BackLink label="Back to roles" to="/roles" />
      <DetailHeader
        icon="shield"
        title={role.displayName}
        description={role.description ?? role.name}
        badge={
          role.isSystemRole ? (
            <Badge color="gray" variant="outline">
              System
            </Badge>
          ) : undefined
        }
      />

      <MetaStrip
        items={[
          { label: 'Permissions', value: role.permissions.length },
          { label: 'Type', value: role.isSystemRole ? 'System' : 'Custom' },
          { label: 'Status', value: role.isActive ? 'Active' : 'Inactive' },
        ]}
      />

      <Tabs defaultValue="permissions" color="blue" keepMounted={false}>
        <Tabs.List mb="lg">
          <Tabs.Tab value="permissions">Permissions ({role.permissions.length})</Tabs.Tab>
          <Tabs.Tab value="settings">Settings</Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="permissions">
          <RolePermissions role={role} canEdit={canWrite && !role.isSystemRole} />
        </Tabs.Panel>

        <Tabs.Panel value="settings">
          <RoleSettings role={role} canWrite={canWrite && !role.isSystemRole} onDeleted={() => navigate('/roles')} />
        </Tabs.Panel>
      </Tabs>
    </div>
  );
}

const isWildcardPermission = (permission: string) => permission === '*' || permission.endsWith(':*');

function RolePermissions({ role, canEdit }: { role: Role; canEdit: boolean }) {
  const queryClient = useQueryClient();
  const [permToAdd, setPermToAdd] = useState<string | null>(null);
  // A wildcard grant is broad and irreversible at a click; hold it for explicit confirmation
  // before saving (which is what acknowledges the wildcard to the API).
  const [pendingWildcard, setPendingWildcard] = useState<string | null>(null);
  const families = groupPermissions(role.permissions);

  const catalogQuery = useQuery({
    queryKey: ['permission-catalog'],
    queryFn: () => api.roles.getPlatformPermissionCatalog(),
    enabled: canEdit,
  });

  const save = useMutation({
    mutationFn: (permissions: string[]) =>
      api.roles.updateRole(role.id, {
        displayName: role.displayName,
        description: role.description ?? null,
        permissions,
        acknowledgeWildcardGrant: permissions.some((permission) => permission.endsWith(':*') || permission === '*'),
      }),
    onSuccess: () => {
      notifications.show({ message: 'Permissions updated', color: 'green' });
      setPermToAdd(null);
      setPendingWildcard(null);
      void queryClient.invalidateQueries({ queryKey: ['role', role.id] });
      void queryClient.invalidateQueries({ queryKey: ['roles'] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const addPermission = (permission: string) => {
    if (isWildcardPermission(permission)) {
      setPendingWildcard(permission);
      return;
    }
    save.mutate([...role.permissions, permission]);
  };

  const assignable = (catalogQuery.data?.items ?? [])
    .filter((item) => item.assignable && !role.permissions.includes(item.permission))
    .map((item) => ({ value: item.permission, label: `${item.permission} — ${item.displayName}` }));

  return (
    <SectionCard
      title="Platform permissions"
      description="Console RBAC actions this role grants across the admin domains."
      right={
        canEdit ? (
          <Group gap="xs" wrap="nowrap">
            <Select
              placeholder="Add a permission"
              searchable
              w={260}
              data={assignable}
              value={permToAdd}
              onChange={setPermToAdd}
              nothingFoundMessage="No assignable permissions"
            />
            <Button
              disabled={!permToAdd}
              loading={save.isPending}
              onClick={() => permToAdd && addPermission(permToAdd)}
            >
              Add
            </Button>
          </Group>
        ) : undefined
      }
    >
      {families.length === 0 ? (
        <Text c="dimmed" size="sm">
          This role grants no permissions.
        </Text>
      ) : (
        <Stack gap="md">
          {families.map((family) => (
            <Group key={family.family} align="center" gap="md" wrap="nowrap">
              <Text className="mw-mono" fw={600} size="sm" style={{ width: 160, flexShrink: 0 }}>
                {family.family}
              </Text>
              <Group gap={6}>
                {family.items.map((item) => (
                  <Badge
                    key={item.key}
                    color="green"
                    size="sm"
                    variant="light"
                    leftSection={<Icon name="check" size={12} />}
                    rightSection={
                      canEdit ? (
                        <ActionIcon
                          aria-label={`Remove ${item.key}`}
                          color="green"
                          size="xs"
                          variant="transparent"
                          onClick={() => save.mutate(role.permissions.filter((permission) => permission !== item.key))}
                        >
                          <Icon name="x" size={11} />
                        </ActionIcon>
                      ) : undefined
                    }
                  >
                    {item.action}
                  </Badge>
                ))}
              </Group>
            </Group>
          ))}
        </Stack>
      )}

      <ConfirmModal
        opened={pendingWildcard !== null}
        title="Grant a wildcard permission?"
        message={`"${pendingWildcard ?? ''}" grants every action in its scope. This is a broad, high-impact grant — confirm you intend to acknowledge it for this role.`}
        confirmLabel="Grant wildcard"
        danger={false}
        loading={save.isPending}
        onConfirm={() => pendingWildcard && save.mutate([...role.permissions, pendingWildcard])}
        onClose={() => setPendingWildcard(null)}
      />
    </SectionCard>
  );
}

function RoleSettings({ role, canWrite, onDeleted }: { role: Role; canWrite: boolean; onDeleted: () => void }) {
  const queryClient = useQueryClient();
  const form = useForm({
    initialValues: { displayName: role.displayName, description: role.description ?? '' },
    validate: { displayName: (value) => (value.trim() ? null : 'Required') },
  });

  useEffect(() => {
    form.setValues({ displayName: role.displayName, description: role.description ?? '' });
    form.resetDirty({ displayName: role.displayName, description: role.description ?? '' });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [role.id]);

  const save = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.roles.updateRole(role.id, {
        displayName: values.displayName,
        description: values.description || null,
        permissions: role.permissions,
        acknowledgeWildcardGrant: false,
      }),
    onSuccess: () => {
      notifications.show({ message: 'Role updated', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['role', role.id] });
      void queryClient.invalidateQueries({ queryKey: ['roles'] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const remove = useMutation({
    mutationFn: () => api.roles.deleteRole(role.id),
    onSuccess: () => {
      notifications.show({ message: 'Role deleted', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['roles'] });
      onDeleted();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Stack gap="lg">
      <SectionCard title="General">
        <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
          <Stack gap="md">
            <TextInput label="Display name" disabled={!canWrite} {...form.getInputProps('displayName')} />
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
        <SectionCard title="Danger zone" description="Deleting a role removes the permissions it grants from its members." danger>
          <Button
            color="red"
            variant="light"
            leftSection={<Icon name="trash-2" size={16} />}
            loading={remove.isPending}
            onClick={() => remove.mutate()}
          >
            Delete role
          </Button>
        </SectionCard>
      )}
    </Stack>
  );
}
