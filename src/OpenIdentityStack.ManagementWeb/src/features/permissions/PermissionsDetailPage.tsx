import { Badge, Box, Button, Stack, Tabs, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router';
import type { DelegatedMaintainer, RegisteredApplication, RegisteredApplicationPermission } from '@openidentitystack/admin-api-client';
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
  const canWrite = hasPermission(auth.permissions, 'application-permissions:write');

  const query = useQuery({
    queryKey: ['application-permission', registrationId],
    queryFn: () => api.applicationPermissions.getRegisteredApplication(registrationId),
  });

  const lifecycle = useMutation({
    mutationFn: (app: RegisteredApplication) =>
      api.applicationPermissions.changeApplicationLifecycle(app.id, {
        status: app.status === 'Disabled' ? 'Active' : 'Disabled',
        acknowledgeDependencies: true,
        concurrencyToken: app.concurrencyToken,
      }),
    onSuccess: () => {
      notifications.show({ message: 'Application updated', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['application-permission', registrationId] });
      void queryClient.invalidateQueries({ queryKey: ['application-permissions'] });
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
        </Tabs.List>

        <Tabs.Panel value="permissions">
          <SectionCard title="Declared permissions" description="Scopes this application declares for roles to grant.">
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
          <SectionCard title="Delegated maintainers" description="Principals allowed to manage this application's permission manifest.">
            {app.maintainers.length === 0 ? (
              <Stack gap={4}>
                <Text c="dimmed" size="sm">
                  No delegated maintainers. The owner manages this application.
                </Text>
                <Box>
                  <Text c="dimmed" size="xs">
                    Owner: {app.ownerType} · <Text component="span" className="mw-mono">{app.ownerId}</Text>
                  </Text>
                </Box>
              </Stack>
            ) : (
              <DataTable
                columns={maintainerColumns}
                rows={app.maintainers}
                getRowKey={(maintainer) => maintainer.id}
                minWidth={400}
              />
            )}
          </SectionCard>
        </Tabs.Panel>
      </Tabs>
    </div>
  );
}
