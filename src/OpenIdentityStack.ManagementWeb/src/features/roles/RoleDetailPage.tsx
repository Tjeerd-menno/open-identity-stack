import { Badge, Button, Group, Stack, Tabs, Text, TextInput, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router';
import type { Role } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { BackLink, CenteredState, DetailHeader, ErrorState, MetaStrip, SectionCard } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';

/** Group "family:action" permission strings by their resource family. */
function groupPermissions(permissions: string[]): Array<{ family: string; actions: string[] }> {
  const byFamily = new Map<string, string[]>();
  for (const permission of permissions) {
    const [family, action = '*'] = permission.split(':');
    byFamily.set(family, [...(byFamily.get(family) ?? []), action]);
  }
  return [...byFamily.entries()].map(([family, actions]) => ({ family, actions }));
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
  const families = groupPermissions(role.permissions);

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
          <SectionCard title="Platform permissions" description="Console RBAC actions this role grants across the admin domains.">
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
                      {family.actions.map((action) => (
                        <Badge key={action} color="green" size="sm" variant="light" leftSection={<Icon name="check" size={12} />}>
                          {action}
                        </Badge>
                      ))}
                    </Group>
                  </Group>
                ))}
              </Stack>
            )}
          </SectionCard>
        </Tabs.Panel>

        <Tabs.Panel value="settings">
          <RoleSettings role={role} canWrite={canWrite && !role.isSystemRole} onDeleted={() => navigate('/roles')} />
        </Tabs.Panel>
      </Tabs>
    </div>
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
