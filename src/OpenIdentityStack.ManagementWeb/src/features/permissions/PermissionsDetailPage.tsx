import { Badge, Box, Button, Code, Group, Modal, Select, Stack, Tabs, Text, TextInput, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useParams } from 'react-router';
import type {
  ApplicationPermissionHistory,
  DelegatedMaintainer,
  PermissionDiagnostics,
  PrincipalType,
  RegisteredApplication,
  RegisteredApplicationPermission,
} from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { DataTable, type Column } from '@/components/DataTable';
import { BackLink, CenteredState, DetailHeader, ErrorState, MetaStrip, SectionCard, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';

export function PermissionsDetailPage() {
  const { registrationId = '' } = useParams();
  const auth = useAuth();
  const queryClient = useQueryClient();
  // Registry maintenance (edit, add permission) needs application-permissions:write; ownership
  // transfer and maintainer management are guarded by application-permissions:admin.
  const canWrite = hasPermission(auth.permissions, 'application-permissions:write');
  const canAdmin = hasPermission(auth.permissions, 'application-permissions:admin');

  const [addPermissionOpened, addPermissionControls] = useDisclosure(false);

  const query = useQuery({
    queryKey: ['application-permission', registrationId],
    queryFn: () => api.applicationPermissions.getRegisteredApplication(registrationId),
  });
  const historyQuery = useQuery({
    queryKey: ['application-permission', registrationId, 'history'],
    // applicationIdentifier is the slug (e.g. "patient-api"), not the GUID; the endpoint
    // filters by RegisteredApplication.ApplicationIdentifier, so pass the loaded value.
    queryFn: () => api.applicationPermissions.getApplicationPermissionHistory({ applicationIdentifier: query.data?.applicationIdentifier ?? undefined }),
    enabled: canAdmin && !!query.data,
  });
  const diagnosticsQuery = useQuery({
    queryKey: ['application-permission', 'diagnostics'],
    queryFn: () => api.applicationPermissions.getApplicationPermissionDiagnostics(),
    enabled: canAdmin,
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['application-permission', registrationId] });
    void queryClient.invalidateQueries({ queryKey: ['application-permissions'] });
  };

  const lifecycle = useMutation({
    mutationFn: (app: RegisteredApplication) =>
      api.applicationPermissions.changeApplicationLifecycle(app.id, {
        status: app.status === 'Disabled' ? 'Active' : 'Disabled',
        acknowledgeDependencies: true,
        concurrencyToken: app.concurrencyToken,
      }),
    onSuccess: () => {
      notifications.show({ message: 'Application updated', color: 'green' });
      invalidate();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const removeMaintainer = useMutation({
    mutationFn: (maintainer: DelegatedMaintainer) =>
      api.applicationPermissions.removeDelegatedMaintainer(
        registrationId,
        maintainer.principalId,
        query.data?.concurrencyToken,
      ),
    onSuccess: () => {
      notifications.show({ message: 'Maintainer removed', color: 'green' });
      invalidate();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  if (query.isLoading) {
    return <CenteredState loading title="Loading application…" />;
  }
  if (query.isError || !query.data) {
    return <ErrorState message={getApiErrorMessage(query.error)} />;
  }

  const app = query.data;

  const permissionColumns: Column<RegisteredApplicationPermission>[] = [
    {
      key: 'key',
      header: 'Permission',
      render: (permission) => (
        <Text className="mw-mono" fw={600} size="sm">
          {permission.fullPermissionKey}
        </Text>
      ),
    },
    {
      key: 'name',
      header: 'Display name',
      render: (permission) => <Text size="sm">{permission.displayName ?? '—'}</Text>,
    },
    {
      key: 'category',
      header: 'Category',
      render: (permission) =>
        permission.category ? (
          <Badge color="gray" variant="outline">
            {permission.category}
          </Badge>
        ) : (
          <Text c="dimmed" size="sm">
            —
          </Text>
        ),
    },
  ];

  const maintainerColumns: Column<DelegatedMaintainer>[] = [
    {
      key: 'principal',
      header: 'Principal',
      render: (maintainer) => (
        <Text className="mw-mono" size="sm">
          {maintainer.principalId}
        </Text>
      ),
    },
    {
      key: 'type',
      header: 'Type',
      render: (maintainer) => (
        <Badge color={maintainer.principalType === 'Group' ? 'grape' : 'blue'} variant="light">
          {maintainer.principalType}
        </Badge>
      ),
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      width: 110,
      render: (maintainer) =>
        canAdmin ? (
          <Button
            color="red"
            size="xs"
            variant="subtle"
            loading={removeMaintainer.isPending && removeMaintainer.variables?.id === maintainer.id}
            onClick={() => removeMaintainer.mutate(maintainer)}
          >
            Remove
          </Button>
        ) : null,
    },
  ];

  return (
    <div>
      <BackLink label="Back to permissions" to="/application-permissions" />
      <DetailHeader
        icon="list-checks"
        title={app.displayName}
        description={app.applicationIdentifier}
        badge={<StatusBadge status={app.status} />}
        actions={
          canWrite ? (
            <Button
              variant="default"
              loading={lifecycle.isPending}
              leftSection={<Icon name={app.status === 'Disabled' ? 'power' : 'ban'} size={16} />}
              onClick={() => lifecycle.mutate(app)}
            >
              {app.status === 'Disabled' ? 'Enable' : 'Disable'}
            </Button>
          ) : undefined
        }
      />

      <MetaStrip
        items={[
          { label: 'Permissions', value: app.permissions.length },
          { label: 'Owner', value: `${app.ownerType}` },
          { label: 'Manifest', value: app.manifestVersion ?? '—' },
        ]}
      />

      <Tabs defaultValue="permissions" color="blue" keepMounted={false}>
        <Tabs.List mb="lg">
          <Tabs.Tab value="permissions">Permissions ({app.permissions.length})</Tabs.Tab>
          <Tabs.Tab value="maintainers">Maintainers ({app.maintainers.length})</Tabs.Tab>
          <Tabs.Tab value="settings">Settings</Tabs.Tab>
          {canAdmin && <Tabs.Tab value="history">History</Tabs.Tab>}
          {canAdmin && <Tabs.Tab value="diagnostics">Diagnostics</Tabs.Tab>}
        </Tabs.List>

        <Tabs.Panel value="permissions">
          <SectionCard
            title="Declared permissions"
            description="Scopes this application declares for roles to grant."
            right={
              canWrite ? (
                <Button size="xs" leftSection={<Icon name="plus" size={14} />} onClick={addPermissionControls.open}>
                  Add permission
                </Button>
              ) : undefined
            }
          >
            {app.description && (
              <Text c="dimmed" mb="md" size="sm">
                {app.description}
              </Text>
            )}
            <DataTable
              columns={permissionColumns}
              rows={app.permissions}
              getRowKey={(permission) => permission.id}
              emptyIcon="list-checks"
              emptyTitle="No permissions declared"
              emptyText="This application has not declared any permissions yet."
              minWidth={560}
            />
          </SectionCard>
        </Tabs.Panel>

        <Tabs.Panel value="maintainers">
          <Stack gap="lg">
            <SectionCard title="Delegated maintainers" description="Principals allowed to manage this application's permission manifest.">
              <Box mb={app.maintainers.length === 0 ? 0 : 'md'}>
                <Text c="dimmed" size="xs">
                  Owner: {app.ownerType} · <Text component="span" className="mw-mono">{app.ownerId}</Text>
                </Text>
              </Box>
              {app.maintainers.length === 0 ? (
                <Text c="dimmed" size="sm">
                  No delegated maintainers. The owner manages this application.
                </Text>
              ) : (
                <DataTable
                  columns={maintainerColumns}
                  rows={app.maintainers}
                  getRowKey={(maintainer) => maintainer.id}
                  minWidth={400}
                />
              )}
            </SectionCard>

            {canAdmin && <AddMaintainerCard app={app} onChanged={invalidate} />}
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="settings">
          <Stack gap="lg">
            <RegistrationSettings app={app} canWrite={canWrite} onSaved={invalidate} />
            {canAdmin && <TransferOwnershipCard app={app} onChanged={invalidate} />}
          </Stack>
        </Tabs.Panel>

        {canAdmin && (
          <Tabs.Panel value="history">
            <PermissionHistoryPanel data={historyQuery.data} isLoading={historyQuery.isLoading} />
          </Tabs.Panel>
        )}

        {canAdmin && (
          <Tabs.Panel value="diagnostics">
            <PermissionDiagnosticsPanel data={diagnosticsQuery.data} isLoading={diagnosticsQuery.isLoading} />
          </Tabs.Panel>
        )}
      </Tabs>

      {addPermissionOpened && (
        <AddPermissionModal app={app} onAdded={invalidate} onClose={addPermissionControls.close} />
      )}
    </div>
  );
}

function RegistrationSettings({ app, canWrite, onSaved }: { app: RegisteredApplication; canWrite: boolean; onSaved: () => void }) {
  const form = useForm({
    initialValues: {
      displayName: app.displayName,
      description: app.description ?? '',
      manifestBaseUrl: app.manifestBaseUrl ?? '',
    },
    validate: { displayName: (value) => (value.trim() ? null : 'Required') },
  });

  useEffect(() => {
    const next = {
      displayName: app.displayName,
      description: app.description ?? '',
      manifestBaseUrl: app.manifestBaseUrl ?? '',
    };
    form.setValues(next);
    form.resetDirty(next);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [app.id, app.displayName, app.description, app.manifestBaseUrl]);

  const save = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.applicationPermissions.updateRegisteredApplication(app.id, {
        displayName: values.displayName,
        description: values.description || null,
        manifestBaseUrl: values.manifestBaseUrl || null,
        concurrencyToken: app.concurrencyToken,
      }),
    onSuccess: () => {
      notifications.show({ message: 'Application updated', color: 'green' });
      onSaved();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <SectionCard title="General">
      <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
        <Stack gap="md">
          <TextInput label="Display name" disabled={!canWrite} {...form.getInputProps('displayName')} />
          <Textarea label="Description" autosize minRows={2} disabled={!canWrite} {...form.getInputProps('description')} />
          <TextInput
            label="Manifest base URL"
            description="Base URL used when importing this application's permission manifest."
            placeholder="https://api.example.com"
            disabled={!canWrite}
            {...form.getInputProps('manifestBaseUrl')}
          />
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
  );
}

function AddMaintainerCard({ app, onChanged }: { app: RegisteredApplication; onChanged: () => void }) {
  const form = useForm({
    initialValues: { principalId: '', principalType: 'User' as PrincipalType },
    validate: { principalId: (value) => (value.trim() ? null : 'Required') },
  });

  const add = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.applicationPermissions.addDelegatedMaintainer(app.id, {
        principalId: values.principalId.trim(),
        principalType: values.principalType,
        concurrencyToken: app.concurrencyToken,
      }),
    onSuccess: () => {
      notifications.show({ message: 'Maintainer added', color: 'green' });
      form.reset();
      onChanged();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <SectionCard title="Add maintainer" description="Grant a user or group permission to manage this application's manifest.">
      <form onSubmit={form.onSubmit((values) => add.mutate(values))}>
        <Group align="flex-end" gap="sm" wrap="nowrap">
          <Select
            label="Type"
            data={['User', 'Group']}
            allowDeselect={false}
            w={140}
            value={form.values.principalType}
            onChange={(value) => value && form.setFieldValue('principalType', value as PrincipalType)}
          />
          <TextInput label="Principal ID" placeholder="user or group id" style={{ flex: 1 }} {...form.getInputProps('principalId')} />
          <Button type="submit" leftSection={<Icon name="plus" size={14} />} loading={add.isPending}>
            Add
          </Button>
        </Group>
      </form>
    </SectionCard>
  );
}

function TransferOwnershipCard({ app, onChanged }: { app: RegisteredApplication; onChanged: () => void }) {
  const form = useForm({
    initialValues: { ownerId: '', ownerType: 'User' as PrincipalType },
    validate: { ownerId: (value) => (value.trim() ? null : 'Required') },
  });

  const transfer = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.applicationPermissions.transferApplicationOwnership(app.id, {
        ownerId: values.ownerId.trim(),
        ownerType: values.ownerType,
        concurrencyToken: app.concurrencyToken,
      }),
    onSuccess: () => {
      notifications.show({ message: 'Ownership transferred', color: 'green' });
      form.reset();
      onChanged();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <SectionCard
      title="Transfer ownership"
      description="Move ownership of this registered application to another principal. The current owner can no longer manage it afterwards."
      danger
    >
      <form onSubmit={form.onSubmit((values) => transfer.mutate(values))}>
        <Group align="flex-end" gap="sm" wrap="nowrap">
          <Select
            label="New owner type"
            data={['User', 'Group']}
            allowDeselect={false}
            w={150}
            value={form.values.ownerType}
            onChange={(value) => value && form.setFieldValue('ownerType', value as PrincipalType)}
          />
          <TextInput label="New owner ID" placeholder="user or group id" style={{ flex: 1 }} {...form.getInputProps('ownerId')} />
          <Button type="submit" color="red" variant="light" loading={transfer.isPending}>
            Transfer
          </Button>
        </Group>
      </form>
    </SectionCard>
  );
}

function AddPermissionModal({ app, onAdded, onClose }: { app: RegisteredApplication; onAdded: () => void; onClose: () => void }) {
  const form = useForm({
    initialValues: { permissionKey: '', displayName: '', description: '', category: '' },
    validate: {
      permissionKey: (value) => (value.trim() ? null : 'Required'),
      displayName: (value) => (value.trim() ? null : 'Required'),
    },
  });

  const add = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.applicationPermissions.addApplicationPermission(app.id, {
        permissionKey: values.permissionKey.trim(),
        displayName: values.displayName.trim(),
        description: values.description || null,
        category: values.category || null,
        concurrencyToken: app.concurrencyToken,
      }),
    onSuccess: () => {
      notifications.show({ message: 'Permission added', color: 'green' });
      onAdded();
      onClose();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Modal opened onClose={onClose} title="Add permission" centered>
      <form onSubmit={form.onSubmit((values) => add.mutate(values))}>
        <Stack gap="md">
          <TextInput label="Permission key" placeholder="reports.read" required {...form.getInputProps('permissionKey')} />
          <TextInput label="Display name" placeholder="Read reports" required {...form.getInputProps('displayName')} />
          <Textarea label="Description" autosize minRows={2} {...form.getInputProps('description')} />
          <TextInput label="Category" placeholder="Reporting" {...form.getInputProps('category')} />
          <Group justify="flex-end" gap="sm" mt="xs">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={add.isPending}>
              Add permission
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

function PermissionHistoryPanel({ data, isLoading }: { data: ApplicationPermissionHistory | undefined; isLoading: boolean }) {
  if (isLoading) return <Text c="dimmed" size="sm">Loading…</Text>;

  const removed = data?.removedPermissions ?? [];

  return (
    <SectionCard title="Removed permissions" description="Permissions that were once declared but have since been removed.">
      {removed.length === 0 ? (
        <Text c="dimmed" size="sm">No removed permissions on record.</Text>
      ) : (
        <Stack gap="xs">
          {removed.map((entry) => (
            <Group key={entry.id ?? entry.fullPermissionKey} justify="space-between" wrap="nowrap" gap="md">
              <Text className="mw-mono" size="sm">{entry.fullPermissionKey}</Text>
              {entry.removedAt && (
                <Text c="dimmed" size="xs" style={{ whiteSpace: 'nowrap' }}>
                  Removed {new Date(entry.removedAt).toLocaleDateString()}
                </Text>
              )}
            </Group>
          ))}
        </Stack>
      )}
    </SectionCard>
  );
}

function PermissionDiagnosticsPanel({ data, isLoading }: { data: PermissionDiagnostics | undefined; isLoading: boolean }) {
  if (isLoading) return <Text c="dimmed" size="sm">Loading…</Text>;

  const issues = data?.issues ?? [];

  return (
    <SectionCard
      title="Diagnostics"
      description="Role assignments that reference permissions no longer declared by any registered application."
    >
      {issues.length === 0 ? (
        <Text c="dimmed" size="sm">No diagnostic issues found.</Text>
      ) : (
        <Stack gap="xs">
          {issues.map((issue, index) => (
            <Group key={index} gap="sm" wrap="nowrap">
              <Badge color="red" variant="light">{issue.issueType ?? 'Unknown'}</Badge>
              <Code>{issue.fullPermissionKey}</Code>
              {issue.roleName && <Text c="dimmed" size="sm">in {issue.roleName}</Text>}
            </Group>
          ))}
        </Stack>
      )}
    </SectionCard>
  );
}
