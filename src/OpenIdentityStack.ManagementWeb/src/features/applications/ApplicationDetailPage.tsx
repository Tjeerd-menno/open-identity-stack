import {
  Alert,
  Badge,
  Box,
  Button,
  Checkbox,
  Code,
  CopyButton,
  Group,
  Modal,
  Stack,
  Tabs,
  Text,
  TextInput,
  Textarea,
} from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import type { Application, ApplicationCredential, AddApplicationCertificateRequest } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { DataTable, type Column } from '@/components/DataTable';
import { ConfirmModal } from '@/components/ConfirmModal';
import { BackLink, CenteredState, DetailHeader, ErrorState, FieldRow, MetaStrip, SectionCard, StatusBadge } from '@/components/primitives';
import { EditOAuthModal } from './EditOAuthModal';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import { formatDateTime } from '@/lib/format';
import { useSyncedForm } from '@/lib/use-synced-form';

export function ApplicationDetailPage() {
  const { applicationId = '' } = useParams();
  const navigate = useNavigate();
  const auth = useAuth();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(auth.permissions, 'applications:write');
  // Secret/certificate add/revoke requires specific permissions independent of applications:write.
  const canManageCredentials = hasPermission(auth.permissions, 'applications:manage-credentials');
  const canManageCertificates = hasPermission(auth.permissions, 'applications:manage-certificates');
  const canDelete = hasPermission(auth.permissions, 'applications:delete');

  const appQuery = useQuery({
    queryKey: ['application', applicationId],
    queryFn: () => api.applications.getApplication(applicationId),
  });
  const credentialsQuery = useQuery({
    queryKey: ['application', applicationId, 'credentials'],
    queryFn: () => api.applications.listApplicationCredentials(applicationId),
  });

  const [addSecretOpened, addSecretControls] = useDisclosure(false);
  const [addCertOpened, addCertControls] = useDisclosure(false);
  const [editOAuthOpened, editOAuthControls] = useDisclosure(false);
  const [pendingRevoke, setPendingRevoke] = useState<ApplicationCredential | null>(null);
  const [confirmDelete, deleteControls] = useDisclosure(false);

  const invalidateApp = () => void queryClient.invalidateQueries({ queryKey: ['application', applicationId] });

  const toggleStatus = useMutation({
    mutationFn: (app: Application) =>
      app.status === 'Disabled' ? api.applications.enableApplication(app.id) : api.applications.disableApplication(app.id),
    onSuccess: () => {
      notifications.show({ message: 'Application updated', color: 'green' });
      invalidateApp();
      void queryClient.invalidateQueries({ queryKey: ['applications'] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const revokeCredential = useMutation({
    mutationFn: (credential: ApplicationCredential) => api.applications.revokeApplicationCredential(applicationId, credential.id),
    onSuccess: () => {
      notifications.show({ message: 'Credential revoked', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['application', applicationId, 'credentials'] });
      setPendingRevoke(null);
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const deleteApp = useMutation({
    mutationFn: () => api.applications.deleteApplication(applicationId),
    onSuccess: () => {
      notifications.show({ message: 'Application deleted', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['applications'] });
      navigate('/applications');
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  if (appQuery.isLoading) {
    return <CenteredState loading title="Loading application…" />;
  }
  if (appQuery.isError || !appQuery.data) {
    return <ErrorState message={getApiErrorMessage(appQuery.error)} />;
  }

  const app = appQuery.data;
  const credentials = credentialsQuery.data ?? [];

  const credentialColumns: Column<ApplicationCredential>[] = [
    {
      key: 'type',
      header: 'Type',
      render: (credential) => (
        <Group gap="sm" wrap="nowrap">
          <Icon name="key-round" size={16} color="var(--mw-text-muted)" />
          <Text fw={600} size="sm">
            {credential.type === 'ClientSecret' ? 'Client secret' : 'Certificate'}
          </Text>
        </Group>
      ),
    },
    {
      key: 'created',
      header: 'Created',
      render: (credential) => <Text c="dimmed" size="sm">{formatDateTime(credential.createdAt)}</Text>,
    },
    {
      key: 'expires',
      header: 'Expires',
      render: (credential) => (
        <Text c="dimmed" size="sm">
          {credential.expiresAt ? formatDateTime(credential.expiresAt) : 'Never'}
        </Text>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      render: (credential) => <StatusBadge status={credential.revokedAt ? 'Revoked' : 'Active'} />,
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      width: 110,
      render: (credential) =>
        canManageCredentials && !credential.revokedAt ? (
          <Button color="red" size="xs" variant="subtle" onClick={() => setPendingRevoke(credential)}>
            Revoke
          </Button>
        ) : null,
    },
  ];

  return (
    <div>
      <BackLink label="Back to applications" to="/applications" />
      <DetailHeader
        icon={app.allowedGrantTypes.includes('client_credentials') ? 'server' : 'app-window'}
        title={app.displayName}
        description={app.clientId}
        badge={<StatusBadge status={app.status} />}
        actions={
          canWrite ? (
            <>
              <Button variant="default" leftSection={<Icon name="pencil" size={16} />} onClick={editOAuthControls.open}>
                Edit configuration
              </Button>
              <Button
                variant="default"
                loading={toggleStatus.isPending}
                leftSection={<Icon name={app.status === 'Disabled' ? 'power' : 'ban'} size={16} />}
                onClick={() => toggleStatus.mutate(app)}
              >
                {app.status === 'Disabled' ? 'Enable' : 'Disable'}
              </Button>
            </>
          ) : undefined
        }
      />

      <MetaStrip
        items={[
          { label: 'Profile', value: app.profile },
          { label: 'Client type', value: app.clientType },
          { label: 'Scopes', value: app.allowedScopes.length },
          { label: 'Created', value: formatDateTime(app.createdAt) },
        ]}
      />

      <Tabs defaultValue="config" color="blue" keepMounted={false}>
        <Tabs.List mb="lg">
          <Tabs.Tab value="config">Configuration</Tabs.Tab>
          <Tabs.Tab value="oauth">Scopes &amp; grants</Tabs.Tab>
          <Tabs.Tab value="uris">Redirect URIs</Tabs.Tab>
          <Tabs.Tab value="credentials">Credentials ({credentials.length})</Tabs.Tab>
          <Tabs.Tab value="settings">Settings</Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="config">
          <SectionCard title="Configuration">
            <FieldRow label="Client ID" value={app.clientId} mono />
            <FieldRow label="Profile" value={app.profile} />
            <FieldRow label="Client type" value={app.clientType} />
            <FieldRow label="PKCE" value={app.requirePkce ? 'Required' : 'Not required'} />
            <FieldRow label="Consent" value={app.requireConsent ? 'Required' : 'Not required'} />
            <FieldRow label="Created" value={formatDateTime(app.createdAt)} last />
          </SectionCard>
        </Tabs.Panel>

        <Tabs.Panel value="oauth">
          <Stack gap="lg">
            <SectionCard title="Grant types" description="OAuth flows this client is permitted to use.">
              <Group gap="xs">
                {app.allowedGrantTypes.length === 0 ? (
                  <Text c="dimmed" size="sm">None configured.</Text>
                ) : (
                  app.allowedGrantTypes.map((grant) => (
                    <Badge key={grant} color="gray" variant="outline" style={{ fontFamily: 'var(--mw-mono)' }}>
                      {grant}
                    </Badge>
                  ))
                )}
              </Group>
            </SectionCard>
            <SectionCard title="Allowed scopes" description="Scopes this client may request at the token endpoint.">
              <Group gap="xs">
                {app.allowedScopes.length === 0 ? (
                  <Text c="dimmed" size="sm">No scopes.</Text>
                ) : (
                  app.allowedScopes.map((scope) => (
                    <Badge key={scope} color="blue" variant="light" style={{ fontFamily: 'var(--mw-mono)' }}>
                      {scope}
                    </Badge>
                  ))
                )}
              </Group>
            </SectionCard>
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="uris">
          <Stack gap="lg">
            <SectionCard title="Redirect URIs">
              {app.redirectUris.length === 0 ? (
                <Text c="dimmed" size="sm">None — this client does not redirect a user.</Text>
              ) : (
                <Stack gap={0}>
                  {app.redirectUris.map((uri, index) => (
                    <FieldRow key={uri} label={`URI ${index + 1}`} value={uri} mono last={index === app.redirectUris.length - 1} />
                  ))}
                </Stack>
              )}
            </SectionCard>
            <SectionCard title="Post-logout redirect URIs">
              {app.postLogoutRedirectUris.length === 0 ? (
                <Text c="dimmed" size="sm">None configured.</Text>
              ) : (
                <Stack gap={0}>
                  {app.postLogoutRedirectUris.map((uri, index) => (
                    <FieldRow key={uri} label={`URI ${index + 1}`} value={uri} mono last={index === app.postLogoutRedirectUris.length - 1} />
                  ))}
                </Stack>
              )}
            </SectionCard>
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="credentials">
          <SectionCard
            title="Credentials"
            description="Client secrets and certificates registered to this application."
            right={
              app.clientType === 'Confidential' && (canManageCredentials || canManageCertificates) ? (
                <Group gap="xs">
                  {canManageCredentials && (
                    <Button size="xs" leftSection={<Icon name="plus" size={14} />} onClick={addSecretControls.open}>
                      Add secret
                    </Button>
                  )}
                  {canManageCertificates && (
                    <Button size="xs" variant="default" leftSection={<Icon name="plus" size={14} />} onClick={addCertControls.open}>
                      Add certificate
                    </Button>
                  )}
                </Group>
              ) : undefined
            }
          >
            {app.clientType !== 'Confidential' ? (
              <Text c="dimmed" size="sm">
                Public clients have no client secret — they authenticate with PKCE.
              </Text>
            ) : (
              <DataTable
                columns={credentialColumns}
                rows={credentials}
                getRowKey={(credential) => credential.id}
                isLoading={credentialsQuery.isLoading}
                emptyIcon="key-round"
                emptyTitle="No credentials"
                emptyText="Add a client secret to let this confidential client authenticate."
                minWidth={560}
              />
            )}
          </SectionCard>
        </Tabs.Panel>

        <Tabs.Panel value="settings">
          <AppSettings app={app} canWrite={canWrite} canDelete={canDelete} onEdited={invalidateApp} onDelete={deleteControls.open} />
        </Tabs.Panel>
      </Tabs>

      {addSecretOpened && (
        <AddSecretModal
          applicationId={applicationId}
          onAdded={() => void queryClient.invalidateQueries({ queryKey: ['application', applicationId, 'credentials'] })}
          onClose={addSecretControls.close}
        />
      )}
      {addCertOpened && (
        <AddCertificateModal
          applicationId={applicationId}
          onAdded={() => void queryClient.invalidateQueries({ queryKey: ['application', applicationId, 'credentials'] })}
          onClose={addCertControls.close}
        />
      )}
      {editOAuthOpened && <EditOAuthModal app={app} onSaved={invalidateApp} onClose={editOAuthControls.close} />}
      <ConfirmModal
        opened={pendingRevoke !== null}
        title="Revoke credential"
        message="Revoking this credential immediately stops it from working. Clients using it must switch to a new one."
        confirmLabel="Revoke"
        loading={revokeCredential.isPending}
        onConfirm={() => pendingRevoke && revokeCredential.mutate(pendingRevoke)}
        onClose={() => setPendingRevoke(null)}
      />
      <ConfirmModal
        opened={confirmDelete}
        title="Delete application"
        message={`Permanently delete ${app.displayName}? Tokens issued to it will stop working.`}
        confirmLabel="Delete application"
        loading={deleteApp.isPending}
        onConfirm={() => deleteApp.mutate()}
        onClose={deleteControls.close}
      />
    </div>
  );
}

function AppSettings({
  app,
  canWrite,
  canDelete,
  onEdited,
  onDelete,
}: {
  app: Application;
  canWrite: boolean;
  canDelete: boolean;
  onEdited: () => void;
  onDelete: () => void;
}) {
  const queryClient = useQueryClient();
  const form = useForm({
    initialValues: { displayName: app.displayName, description: app.description ?? '' },
    validate: { displayName: (value) => (value.trim() ? null : 'Required') },
  });

  useSyncedForm(form, { displayName: app.displayName, description: app.description ?? '' }, app.id);

  const save = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.applications.updateApplicationMetadata(app.id, {
        displayName: values.displayName,
        description: values.description || null,
      }),
    onSuccess: () => {
      notifications.show({ message: 'Application updated', color: 'green' });
      onEdited();
      void queryClient.invalidateQueries({ queryKey: ['applications'] });
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

      {canDelete && (
        <SectionCard title="Danger zone" description="Deleting an application invalidates its credentials and any tokens it has issued." danger>
          <Button color="red" variant="light" leftSection={<Icon name="trash-2" size={16} />} onClick={onDelete}>
            Delete application
          </Button>
        </SectionCard>
      )}
    </Stack>
  );
}

function AddSecretModal({ applicationId, onAdded, onClose }: { applicationId: string; onAdded: () => void; onClose: () => void }) {
  const [secret, setSecret] = useState<string | null>(null);
  const form = useForm({ initialValues: { description: '', revokeExisting: false } });

  const mutation = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.applications.addApplicationSecret(applicationId, {
        description: values.description || null,
        revokeExisting: values.revokeExisting,
      }),
    onSuccess: (response) => {
      setSecret(response.clientSecret);
      onAdded();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  if (secret) {
    return (
      <Modal opened onClose={onClose} title="Client secret created" centered>
        <Stack gap="md">
          <Alert color="green" icon={<Icon name="circle-check" size={18} />} variant="light">
            A new client secret was generated.
          </Alert>
          <Box>
            <Text fw={600} size="sm">
              Client secret
            </Text>
            <Text c="dimmed" size="xs" mb={6}>
              Copy it now — this secret won't be shown again.
            </Text>
            <Group gap="xs" wrap="nowrap">
              <Code style={{ flex: 1, wordBreak: 'break-all', padding: 8 }}>{secret}</Code>
              <CopyButton value={secret}>
                {({ copied, copy }) => (
                  <Button
                    variant="light"
                    color={copied ? 'green' : 'blue'}
                    leftSection={<Icon name={copied ? 'check' : 'copy'} size={15} />}
                    onClick={copy}
                  >
                    {copied ? 'Copied' : 'Copy'}
                  </Button>
                )}
              </CopyButton>
            </Group>
          </Box>
          <Group justify="flex-end">
            <Button onClick={onClose}>Done</Button>
          </Group>
        </Stack>
      </Modal>
    );
  }

  return (
    <Modal opened onClose={onClose} title="Add client secret" centered>
      <form onSubmit={form.onSubmit((values) => mutation.mutate(values))}>
        <Stack gap="md">
          <TextInput label="Description" placeholder="CI deploy key" {...form.getInputProps('description')} />
          <Checkbox label="Revoke existing secrets" {...form.getInputProps('revokeExisting', { type: 'checkbox' })} />
          <Group justify="flex-end" gap="sm" mt="xs">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={mutation.isPending}>
              Generate secret
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}

function AddCertificateModal({ applicationId, onAdded, onClose }: { applicationId: string; onAdded: () => void; onClose: () => void }) {
  const form = useForm<AddApplicationCertificateRequest>({
    initialValues: { thumbprint: '', subject: '', description: '', expiresAt: '' },
    validate: { thumbprint: (value) => (value.trim() ? null : 'Required') },
  });

  const mutation = useMutation({
    mutationFn: (values: AddApplicationCertificateRequest) =>
      api.applications.addApplicationCertificate(applicationId, {
        thumbprint: values.thumbprint.trim(),
        subject: values.subject?.trim() || null,
        description: values.description?.trim() || null,
        expiresAt: values.expiresAt?.trim() || null,
      }),
    onSuccess: () => {
      notifications.show({ message: 'Certificate registered', color: 'green' });
      onAdded();
      onClose();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Modal opened onClose={onClose} title="Add certificate" centered>
      <form onSubmit={form.onSubmit((values) => mutation.mutate(values))}>
        <Stack gap="md">
          <TextInput
            label="Thumbprint"
            description="SHA-1 or SHA-256 hex thumbprint of the certificate."
            placeholder="a1b2c3d4…"
            styles={{ input: { fontFamily: 'var(--mw-mono)' } }}
            required
            {...form.getInputProps('thumbprint')}
          />
          <TextInput label="Subject" placeholder="CN=my-service" {...form.getInputProps('subject')} />
          <TextInput label="Description" placeholder="Production signing cert" {...form.getInputProps('description')} />
          <TextInput label="Expires at" type="date" {...form.getInputProps('expiresAt')} />
          <Group justify="flex-end" gap="sm" mt="xs">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={mutation.isPending}>
              Register certificate
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
