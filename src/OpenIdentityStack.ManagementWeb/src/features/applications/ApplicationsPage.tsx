import { Badge, Box, Group, Text, ThemeIcon } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import type {
  ApplicationListItem,
  ApplicationProfile,
  ApplicationStatus,
} from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterToolbar } from '@/components/FilterToolbar';
import { Pager } from '@/components/ListControls';
import { RowMenu } from '@/components/RowMenu';
import { ConfirmModal } from '@/components/ConfirmModal';
import { ErrorState, PageHeader, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import { formatRelativeTime } from '@/lib/format';

const PROFILE_LABELS: Record<string, string> = {
  Web: 'Web',
  SinglePage: 'Single page',
  Native: 'Native',
  MachineToMachine: 'Machine-to-machine',
  Device: 'Device',
  Custom: 'Custom',
};

const PROFILE_COLORS: Record<string, string> = {
  Web: 'blue',
  SinglePage: 'teal',
  Native: 'cyan',
  MachineToMachine: 'orange',
  Device: 'grape',
  Custom: 'gray',
};

export function ApplicationsPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(auth.permissions, 'applications:write');
  const [search, setSearch] = useState('');
  const [profile, setProfile] = useState('All');
  const [status, setStatus] = useState('All');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [pendingDelete, setPendingDelete] = useState<ApplicationListItem | null>(null);

  const query = useQuery({
    queryKey: ['applications', { page, pageSize, search, profile, status }],
    queryFn: () =>
      api.applications.getApplications({
        page,
        pageSize,
        search: search || undefined,
        profile: profile === 'All' ? undefined : (profile as ApplicationProfile),
        status: status === 'All' ? undefined : (status as ApplicationStatus),
      }),
  });

  const toggle = useMutation({
    mutationFn: (app: ApplicationListItem) =>
      app.status === 'Disabled' ? api.applications.enableApplication(app.id) : api.applications.disableApplication(app.id),
    onSuccess: () => {
      notifications.show({ message: 'Application updated', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['applications'] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const remove = useMutation({
    mutationFn: (app: ApplicationListItem) => api.applications.deleteApplication(app.id),
    onSuccess: () => {
      notifications.show({ message: 'Application deleted', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['applications'] });
      setPendingDelete(null);
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const resetPage = () => setPage(1);

  const columns: Column<ApplicationListItem>[] = [
    {
      key: 'app',
      header: 'Application',
      render: (app) => (
        <Group gap="sm" wrap="nowrap">
          <ThemeIcon color={PROFILE_COLORS[app.profile] ?? 'blue'} radius="md" size={36} variant="light">
            <Icon name={app.allowedGrantTypes.includes('client_credentials') ? 'server' : 'app-window'} size={18} />
          </ThemeIcon>
          <Box style={{ minWidth: 0 }}>
            <Text fw={600} size="sm" truncate>
              {app.displayName}
            </Text>
            <Text c="dimmed" className="mw-mono" size="xs" truncate>
              {app.clientId}
            </Text>
          </Box>
        </Group>
      ),
    },
    {
      key: 'profile',
      header: 'Profile',
      render: (app) => (
        <Badge color={PROFILE_COLORS[app.profile] ?? 'blue'} variant="dot">
          {PROFILE_LABELS[app.profile] ?? app.profile}
        </Badge>
      ),
    },
    { key: 'type', header: 'Client type', render: (app) => <Text c="dimmed" size="sm">{app.clientType}</Text> },
    { key: 'status', header: 'Status', render: (app) => <StatusBadge status={app.status} /> },
    {
      key: 'created',
      header: 'Created',
      render: (app) => <Text c="dimmed" size="sm">{formatRelativeTime(app.createdAt)}</Text>,
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (app) => (
        <RowMenu
          items={[
            { label: 'Open', icon: 'arrow-up-right', onClick: () => navigate(`/applications/${app.id}`) },
            {
              label: app.status === 'Disabled' ? 'Enable' : 'Disable',
              icon: 'ban',
              disabled: !canWrite,
              onClick: () => toggle.mutate(app),
            },
            { separator: true },
            { label: 'Delete application', icon: 'trash-2', danger: true, disabled: !canWrite, onClick: () => setPendingDelete(app) },
          ]}
        />
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Applications"
        description="OAuth/OIDC software registrations that can sign users in or request tokens."
      />

      <FilterToolbar
        noun="applications"
        search={search}
        onSearch={(value) => { setSearch(value); resetPage(); }}
        filters={[
          {
            key: 'profile',
            label: 'Profile',
            value: profile,
            options: ['All', 'Web', 'SinglePage', 'Native', 'MachineToMachine', 'Device', 'Custom'],
            onChange: (value) => { setProfile(value); resetPage(); },
          },
          {
            key: 'status',
            label: 'Status',
            value: status,
            options: ['All', 'Active', 'Disabled'],
            onChange: (value) => { setStatus(value); resetPage(); },
          },
        ]}
        rows={pageSize}
        onRows={(size) => { setPageSize(size); resetPage(); }}
        count={query.data?.totalCount}
      />

      {query.isError ? (
        <ErrorState message={getApiErrorMessage(query.error)} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={query.data?.items ?? []}
            getRowKey={(app) => app.id}
            onRowClick={(app) => navigate(`/applications/${app.id}`)}
            isLoading={query.isLoading}
            emptyIcon="app-window"
            emptyTitle="No applications"
            emptyText="Adjust your filters or register an application."
          />
          <Pager
            page={page}
            pageSize={pageSize}
            totalCount={query.data?.totalCount ?? 0}
            totalPages={query.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => { setPageSize(size); resetPage(); }}
          />
        </>
      )}

      <ConfirmModal
        opened={pendingDelete !== null}
        title="Delete application"
        message={`Permanently delete ${pendingDelete?.displayName ?? 'this application'}? Tokens issued to it will stop working.`}
        confirmLabel="Delete application"
        loading={remove.isPending}
        onConfirm={() => pendingDelete && remove.mutate(pendingDelete)}
        onClose={() => setPendingDelete(null)}
      />
    </div>
  );
}
