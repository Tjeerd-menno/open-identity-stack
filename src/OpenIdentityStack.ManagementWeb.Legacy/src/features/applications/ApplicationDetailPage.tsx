import { Alert, Badge, Button, Group, Loader, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { useNavigate, useParams } from 'react-router';
import { ConfirmDialog } from '@/components/ConfirmDialog';
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

  return (
    <Stack gap="lg">
      <Group justify="space-between" align="flex-start">
        <Group align="flex-start">
          <Button variant="default" onClick={() => navigate('/applications')}>Back</Button>
          <div>
            <Title order={1}>{app.displayName}</Title>
            <Text c="dimmed">{app.clientId}</Text>
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
      <Title order={2}>Application information</Title>
      <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
        <Field label="ID" value={application.id} />
        <div>
          <Text size="sm" c="dimmed">Status</Text>
          <ApplicationStatusBadge status={application.status} />
        </div>
        <Field label="Application profile" value={profileLabels[application.profile]} />
        <Field label="Client type" value={clientTypeLabels[application.clientType]} />
        <Field label="Require PKCE" value={application.requirePkce ? 'Yes' : 'No'} />
        <Field label="Require consent" value={application.requireConsent ? 'Yes' : 'No'} />
        <Field label="Created" value={formatDate(application.createdAt)} />
        <Field label="Modified" value={formatDate(application.modifiedAt)} />
      </SimpleGrid>
      {application.description && (
        <div>
          <Text size="sm" c="dimmed">Description</Text>
          <Text>{application.description}</Text>
        </div>
      )}
      {application.requiresMigrationReview && (
        <Alert color="yellow">
          Migration review required{application.migrationSource ? ` from ${application.migrationSource}` : ''}.
        </Alert>
      )}
    </Stack>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <Text size="sm" c="dimmed">{label}</Text>
      <Text>{value}</Text>
    </div>
  );
}

function ValueList({ title, values, emptyMessage }: { title: string; values: string[]; emptyMessage: string }) {
  return (
    <Stack gap="xs">
      <Title order={2}>{title}</Title>
      {values.length === 0 ? (
        <Text c="dimmed">{emptyMessage}</Text>
      ) : (
        values.map((value) => <Text key={value}>{value}</Text>)
      )}
    </Stack>
  );
}

function TagList({ title, values }: { title: string; values: string[] }) {
  return (
    <Stack gap="xs">
      <Title order={2}>{title}</Title>
      <Group>
        {values.map((value) => <Badge key={value} variant="light">{value}</Badge>)}
      </Group>
    </Stack>
  );
}

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString() : 'N/A';
}
