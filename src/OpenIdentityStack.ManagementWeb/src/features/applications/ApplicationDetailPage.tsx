import { Alert, Badge, Button, Group, Loader, Stack, Text, ThemeIcon, Title } from '@mantine/core';
import { useNavigate, useParams } from 'react-router';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { BackLink, DetailCard, FieldRow } from '@/components/DetailPrimitives';
import { AppWindowIcon, ServerIcon } from '@/components/IamIcons';
import { getApiErrorMessage } from '@/lib/admin-api';
import { useState } from 'react';
import { ApplicationClientType, ApplicationProfile, type Application } from './applications-api';
import { ApplicationCredentials } from './ApplicationCredentials';
import { ApplicationStatusBadge } from './ApplicationStatusBadge';
import {
  useApplication,
  useDeleteApplication,
  useDisableApplication,
  useEnableApplication,
} from './applications-hooks';

const profileLabels: Record<ApplicationProfile, string> = {
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

export function ApplicationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const application = useApplication(id);
  const disableApplication = useDisableApplication(id ?? '');
  const enableApplication = useEnableApplication(id ?? '');
  const deleteApplication = useDeleteApplication(id ?? '');
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  if (!id) {
    return <Alert color="red">Application ID not provided.</Alert>;
  }

  if (application.isLoading) {
    return <Loader aria-label="Loading application" />;
  }

  if (application.isError) {
    return <Alert color="red">{getApiErrorMessage(application.error)}</Alert>;
  }

  if (!application.data) {
    return <Alert>Application not found.</Alert>;
  }

  const app = application.data;

  const isMachine = app.profile === ApplicationProfile.MachineToMachine;

  return (
    <Stack gap="lg">
      <BackLink label="Back to Applications" to="/applications" />
      <Group justify="space-between" align="flex-start" gap="md" wrap="wrap">
        <Group gap="md" wrap="nowrap" align="center">
          <ThemeIcon color={isMachine ? 'orange' : 'blue'} variant="light" size={52} radius="md">
            {isMachine ? <ServerIcon style={{ width: 26, height: 26 }} /> : <AppWindowIcon style={{ width: 26, height: 26 }} />}
          </ThemeIcon>
          <div>
            <Group gap="sm" align="center">
              <Title order={1}>{app.displayName}</Title>
              <ApplicationStatusBadge status={app.status} />
            </Group>
            <Text c="dimmed" ff="monospace" mt={2}>{app.clientId}</Text>
          </div>
        </Group>
        <Group>
          <Button variant="default" onClick={() => navigate(`/applications/${id}/edit`)}>Edit</Button>
          {app.status === 'Active' ? (
            <Button variant="default" loading={disableApplication.isPending} onClick={() => disableApplication.mutate()}>
              Disable
            </Button>
          ) : (
            <Button variant="default" loading={enableApplication.isPending} onClick={() => enableApplication.mutate()}>
              Enable
            </Button>
          )}
          <Button color="red" onClick={() => setShowDeleteConfirm(true)}>Delete</Button>
        </Group>
      </Group>

      <ApplicationOverview application={app} />
      <ValueList title="Redirect URIs" values={app.redirectUris} emptyMessage="No redirect URIs configured." />
      <ValueList title="Post Logout Redirect URIs" values={app.postLogoutRedirectUris} emptyMessage="No post logout redirect URIs configured." />
      <TagList title="Allowed scopes" values={app.allowedScopes} />
      <TagList title="Allowed grant types" values={app.allowedGrantTypes} />
      <ApplicationCredentials application={app} />

      <ConfirmDialog
        opened={showDeleteConfirm}
        onOpenChange={setShowDeleteConfirm}
        onConfirm={async () => {
          await deleteApplication.mutateAsync();
          navigate('/applications');
        }}
        title="Delete application"
        message="Are you sure you want to delete this application? This action cannot be undone."
        confirmLabel="Delete application"
        loading={deleteApplication.isPending}
      />
    </Stack>
  );
}

function ApplicationOverview({ application }: { application: Application }) {
  return (
    <Stack gap="md">
      <DetailCard title="Application information">
        <FieldRow label="ID" value={application.id} mono />
        <FieldRow label="Status" value={<ApplicationStatusBadge status={application.status} />} />
        <FieldRow label="Application profile" value={profileLabels[application.profile]} />
        <FieldRow label="Client type" value={clientTypeLabels[application.clientType]} />
        <FieldRow label="Require PKCE" value={application.requirePkce ? 'Yes' : 'No'} />
        <FieldRow label="Require consent" value={application.requireConsent ? 'Yes' : 'No'} />
        <FieldRow label="Created" value={formatDate(application.createdAt)} />
        <FieldRow label="Modified" value={formatDate(application.modifiedAt)} last />
      </DetailCard>
      {application.description && (
        <DetailCard title="Description">
          <Text>{application.description}</Text>
        </DetailCard>
      )}
      {application.requiresMigrationReview && (
        <Alert color="yellow">
          Migration review required{application.migrationSource ? ` from ${application.migrationSource}` : ''}.
        </Alert>
      )}
    </Stack>
  );
}

function ValueList({ title, values, emptyMessage }: { title: string; values: string[]; emptyMessage: string }) {
  return (
    <DetailCard title={title}>
      {values.length === 0 ? (
        <Text c="dimmed">{emptyMessage}</Text>
      ) : (
        <Stack gap="xs">
          {values.map((value) => <Text key={value} ff="monospace" size="sm">{value}</Text>)}
        </Stack>
      )}
    </DetailCard>
  );
}

function TagList({ title, values }: { title: string; values: string[] }) {
  return (
    <DetailCard title={title}>
      <Group gap={6}>
        {values.map((value) => <Badge key={value} variant="light">{value}</Badge>)}
      </Group>
    </DetailCard>
  );
}

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString() : 'N/A';
}
