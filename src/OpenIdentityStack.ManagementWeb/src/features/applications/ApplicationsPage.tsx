import { Badge, Button, Group, Stack, Text, ThemeIcon } from '@mantine/core';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { EntityActionGroup } from '@/components/EntityActionMenu';
import { ApplicationsIcon, ServerIcon } from '@/components/IamIcons';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
import { PageHeader, PageToolbar, type AppliedFilter } from '@/components/PagePrimitives';
import { getApiErrorMessage } from '@/lib/admin-api';
import {
  ApplicationClientType,
  ApplicationProfile,
  type ApplicationListItem,
} from './applications-api';
import { ApplicationStatusBadge } from './ApplicationStatusBadge';
import { useApplications, useDeleteApplication } from './applications-hooks';

const pageSize = 20;

const applicationProfileLabels: Record<ApplicationProfile, string> = {
  [ApplicationProfile.Web]: 'Web',
  [ApplicationProfile.SinglePage]: 'Single Page',
  [ApplicationProfile.Native]: 'Native',
  [ApplicationProfile.MachineToMachine]: 'Machine-to-machine',
  [ApplicationProfile.Device]: 'Device',
  [ApplicationProfile.Custom]: 'Custom',
};

const profileBadgeColors: Record<ApplicationProfile, string> = {
  [ApplicationProfile.Web]: 'blue',
  [ApplicationProfile.SinglePage]: 'teal',
  [ApplicationProfile.Native]: 'cyan',
  [ApplicationProfile.MachineToMachine]: 'orange',
  [ApplicationProfile.Device]: 'grape',
  [ApplicationProfile.Custom]: 'gray',
};

const clientTypeLabels: Record<ApplicationClientType, string> = {
  [ApplicationClientType.Confidential]: 'Confidential',
  [ApplicationClientType.Public]: 'Public',
};

function isMachineToMachine(profile: ApplicationProfile) {
  return profile === ApplicationProfile.MachineToMachine;
}

export function ApplicationsPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [submittedSearch, setSubmittedSearch] = useState('');
  const [profile, setProfile] = useState<ApplicationProfile | ''>('');
  const [status, setStatus] = useState<'Active' | 'Disabled' | ''>('');
  const [clientType, setClientType] = useState<ApplicationClientType | ''>('');
  const [applicationToDelete, setApplicationToDelete] = useState<ApplicationListItem | null>(null);
  const applications = useApplications({
    page,
    pageSize,
    search: submittedSearch || undefined,
    profile: profile || undefined,
    status: status || undefined,
    clientType: clientType || undefined,
  });
  const deleteApplication = useDeleteApplication(applicationToDelete?.id ?? '');

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSubmittedSearch(search);
      setPage(1);
    }, 300);

    return () => window.clearTimeout(timer);
  }, [search]);

  const columns: FoundationColumn<ApplicationListItem>[] = [
    {
      header: 'Application',
      cell: (application) => (
        <Group gap="sm" wrap="nowrap">
          <ThemeIcon
            color={profileBadgeColors[application.profile] ?? 'gray'}
            size={36}
            radius="md"
            variant="light"
          >
            {isMachineToMachine(application.profile) ? <ServerIcon /> : <ApplicationsIcon />}
          </ThemeIcon>
          <Stack gap={2}>
            <Text size="sm" fw={600}>{application.displayName}</Text>
            <Text size="xs" c="dimmed" ff="monospace">{application.clientId}</Text>
          </Stack>
        </Group>
      ),
    },
    {
      header: 'Profile',
      cell: (application) => (
        <Badge
          color={profileBadgeColors[application.profile] ?? 'gray'}
          variant="dot"
          size="sm"
        >
          {applicationProfileLabels[application.profile]}
        </Badge>
      ),
    },
    {
      header: 'Client type',
      cell: (application) => (
        <Text size="sm" c="dimmed">{clientTypeLabels[application.clientType]}</Text>
      ),
    },
    {
      header: 'Status',
      cell: (application) => <ApplicationStatusBadge status={application.status} />,
    },
    {
      header: 'Grant types',
      cell: (application) => (
        <Text size="xs" c="dimmed" ff="monospace">{application.allowedGrantTypes.join(', ')}</Text>
      ),
    },
    {
      header: 'Actions',
      align: 'right',
      cell: (application) => (
        <EntityActionGroup
          actions={[
            { label: `View ${application.displayName}`, onClick: () => navigate(`/applications/${application.id}`) },
            { label: `Edit ${application.displayName}`, onClick: () => navigate(`/applications/${application.id}/edit`) },
            { label: `Delete ${application.displayName}`, color: 'red', onClick: () => setApplicationToDelete(application) },
          ]}
        />
      ),
    },
  ];

  const appliedFilters: AppliedFilter[] = [
    ...(profile ? [{ key: 'profile', label: 'Profile', value: applicationProfileLabels[profile], onRemove: () => setProfile('') }] : []),
    ...(status ? [{ key: 'status', label: 'Status', value: status, onRemove: () => setStatus('') }] : []),
    ...(clientType ? [{ key: 'clientType', label: 'Client type', value: clientTypeLabels[clientType], onRemove: () => setClientType('') }] : []),
  ];

  return (
    <Stack gap="lg">
      <PageHeader
        title="Applications"
        description="OAuth/OIDC software registrations that can sign users in or request tokens."
        actions={<Button onClick={() => navigate('/applications/new')}>New application</Button>}
      />

      <PageToolbar
        searchLabel="Search applications"
        searchPlaceholder="Search by client ID or display name..."
        searchValue={search}
        resultCount={applications.data?.totalCount}
        onSearchChange={setSearch}
        filters={[
          {
            key: 'profile',
            label: 'Profile',
            value: profile,
            onChange: (value) => {
              setProfile((value ?? '') as ApplicationProfile | '');
              setPage(1);
            },
            data: [
              { value: ApplicationProfile.Web, label: 'Web' },
              { value: ApplicationProfile.SinglePage, label: 'Single Page' },
              { value: ApplicationProfile.Native, label: 'Native' },
              { value: ApplicationProfile.MachineToMachine, label: 'Machine-to-machine' },
              { value: ApplicationProfile.Device, label: 'Device' },
              { value: ApplicationProfile.Custom, label: 'Custom' },
            ],
          },
          {
            key: 'status',
            label: 'Status',
            value: status,
            onChange: (value) => {
              setStatus((value ?? '') as 'Active' | 'Disabled' | '');
              setPage(1);
            },
            data: [
              { value: 'Active', label: 'Active' },
              { value: 'Disabled', label: 'Disabled' },
            ],
          },
          {
            key: 'clientType',
            label: 'Client type',
            value: clientType,
            onChange: (value) => {
              setClientType((value ?? '') as ApplicationClientType | '');
              setPage(1);
            },
            data: [
              { value: ApplicationClientType.Confidential, label: 'Confidential' },
              { value: ApplicationClientType.Public, label: 'Public' },
            ],
          },
        ]}
        appliedFilters={appliedFilters}
        onClear={() => {
          setSearch('');
          setSubmittedSearch('');
          setProfile('');
          setStatus('');
          setClientType('');
          setPage(1);
        }}
      />

      <FoundationTable
        columns={columns}
        data={applications.data?.items ?? []}
        isLoading={applications.isLoading}
        error={applications.isError ? getApiErrorMessage(applications.error) : null}
        emptyMessage="No applications found"
        pagination={{
          page,
          pageSize,
          totalCount: applications.data?.totalCount ?? 0,
          totalPages: applications.data?.totalPages ?? 0,
          onPageChange: setPage,
        }}
      />

      <ConfirmDialog
        opened={applicationToDelete !== null}
        onOpenChange={(opened) => !opened && setApplicationToDelete(null)}
        onConfirm={async () => {
          await deleteApplication.mutateAsync();
          setApplicationToDelete(null);
        }}
        title="Delete application"
        message="Are you sure you want to delete this application? This action cannot be undone."
        confirmLabel="Delete"
        loading={deleteApplication.isPending}
      />
    </Stack>
  );
}
