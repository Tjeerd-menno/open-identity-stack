import { Button, Group, NativeSelect, Stack, Text, TextInput, Title } from '@mantine/core';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { FoundationTable, type FoundationColumn } from '@/components/FoundationTable';
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

const clientTypeLabels: Record<ApplicationClientType, string> = {
  [ApplicationClientType.Confidential]: 'Confidential',
  [ApplicationClientType.Public]: 'Public',
};

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
    { header: 'Client ID', accessorKey: 'clientId' },
    { header: 'Display Name', accessorKey: 'displayName' },
    {
      header: 'Profile',
      cell: (application) => applicationProfileLabels[application.profile],
    },
    {
      header: 'Client Type',
      cell: (application) => clientTypeLabels[application.clientType],
    },
    {
      header: 'Status',
      cell: (application) => <ApplicationStatusBadge status={application.status} />,
    },
    {
      header: 'Grant Types',
      cell: (application) => application.allowedGrantTypes.join(', '),
    },
    {
      header: 'Created',
      cell: (application) => new Date(application.createdAt).toLocaleDateString(),
    },
    {
      header: 'Actions',
      cell: (application) => (
        <Group gap="xs" wrap="nowrap">
          <Button variant="subtle" size="xs" onClick={() => navigate(`/applications/${application.id}`)}>
            View
          </Button>
          <Button variant="subtle" size="xs" onClick={() => navigate(`/applications/${application.id}/edit`)}>
            Edit
          </Button>
          <Button
            variant="subtle"
            color="red"
            size="xs"
            aria-label={`Delete ${application.displayName}`}
            onClick={() => setApplicationToDelete(application)}
          >
            Delete
          </Button>
        </Group>
      ),
    },
  ];

  return (
    <Stack gap="lg">
      <Group justify="space-between" align="flex-start">
        <div>
          <Title order={1}>Applications</Title>
          <Text c="dimmed">Manage OAuth 2.0 and OpenID Connect applications.</Text>
        </div>
        <Button onClick={() => navigate('/applications/new')}>New application</Button>
      </Group>

      <Group align="flex-end">
        <TextInput
          maw={420}
          placeholder="Search by client ID or display name..."
          value={search}
          onChange={(event) => setSearch(event.currentTarget.value)}
        />
        <NativeSelect
          label="Profile"
          value={profile}
          onChange={(event) => {
            setProfile(event.currentTarget.value as ApplicationProfile | '');
            setPage(1);
          }}
          data={[
            { value: '', label: 'All profiles' },
            { value: ApplicationProfile.Web, label: 'Web' },
            { value: ApplicationProfile.SinglePage, label: 'Single Page' },
            { value: ApplicationProfile.Native, label: 'Native' },
            { value: ApplicationProfile.MachineToMachine, label: 'Machine-to-machine' },
            { value: ApplicationProfile.Device, label: 'Device' },
            { value: ApplicationProfile.Custom, label: 'Custom' },
          ]}
        />
        <NativeSelect
          label="Status"
          value={status}
          onChange={(event) => {
            setStatus(event.currentTarget.value as 'Active' | 'Disabled' | '');
            setPage(1);
          }}
          data={[
            { value: '', label: 'All statuses' },
            { value: 'Active', label: 'Active' },
            { value: 'Disabled', label: 'Disabled' },
          ]}
        />
        <NativeSelect
          label="Client type"
          value={clientType}
          onChange={(event) => {
            setClientType(event.currentTarget.value as ApplicationClientType | '');
            setPage(1);
          }}
          data={[
            { value: '', label: 'All client types' },
            { value: ApplicationClientType.Confidential, label: 'Confidential' },
            { value: ApplicationClientType.Public, label: 'Public' },
          ]}
        />
      </Group>

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
