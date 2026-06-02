import { Alert, Badge, Button, Checkbox, Group, Modal, Stack, Table, Text, TextInput, Title } from '@mantine/core';
import { useState } from 'react';
import { SecretDisplay } from '@/components/SecretDisplay';
import { getApiErrorMessage } from '@/lib/admin-api';
import { ApplicationClientType, type Application, type ApplicationCredential } from './applications-api';
import {
  useAddApplicationCertificate,
  useAddApplicationSecret,
  useApplicationCredentials,
  useRevokeApplicationCredential,
} from './applications-hooks';

type ApplicationCredentialsProps = {
  application: Application;
};

export function ApplicationCredentials({ application }: ApplicationCredentialsProps) {
  const [showAddSecret, setShowAddSecret] = useState(false);
  const [showAddCertificate, setShowAddCertificate] = useState(false);
  const credentials = useApplicationCredentials(application.id);

  if (application.clientType === ApplicationClientType.Public) {
    return (
      <Stack gap="sm">
        <Title order={2}>Credentials</Title>
        <Alert>Public applications cannot use client secrets or certificates.</Alert>
      </Stack>
    );
  }

  return (
    <Stack gap="md">
      <Group justify="space-between">
        <div>
          <Title order={2}>Credentials</Title>
          <Text c="dimmed">Client secrets and certificates for this confidential application.</Text>
        </div>
        <Group>
          <Button variant="default" onClick={() => setShowAddSecret(true)}>Add secret</Button>
          <Button variant="default" onClick={() => setShowAddCertificate(true)}>Add certificate</Button>
        </Group>
      </Group>

      {credentials.isError && <Alert color="red">{getApiErrorMessage(credentials.error)}</Alert>}
      <CredentialTable applicationId={application.id} credentials={credentials.data ?? []} />

      <AddSecretModal
        applicationId={application.id}
        opened={showAddSecret}
        onClose={() => setShowAddSecret(false)}
      />
      <AddCertificateModal
        applicationId={application.id}
        opened={showAddCertificate}
        onClose={() => setShowAddCertificate(false)}
      />
    </Stack>
  );
}

function CredentialTable({ applicationId, credentials }: { applicationId: string; credentials: ApplicationCredential[] }) {
  const revokeCredential = useRevokeApplicationCredential(applicationId);

  if (credentials.length === 0) {
    return <Text c="dimmed">No credentials configured.</Text>;
  }

  return (
    <Table striped highlightOnHover withTableBorder>
      <Table.Thead>
        <Table.Tr>
          <Table.Th>Credential</Table.Th>
          <Table.Th>Type</Table.Th>
          <Table.Th>Expires</Table.Th>
          <Table.Th>Last used</Table.Th>
          <Table.Th>Actions</Table.Th>
        </Table.Tr>
      </Table.Thead>
      <Table.Tbody>
        {credentials.map((credential) => (
          <Table.Tr key={credential.id}>
            <Table.Td>
              <Text fw={500}>{credential.description ?? credential.subject ?? credential.id}</Text>
              {credential.thumbprint && <Text size="xs" c="dimmed">{credential.thumbprint}</Text>}
              {credential.subject && <Text size="xs" c="dimmed">{credential.subject}</Text>}
            </Table.Td>
            <Table.Td>
              <Badge color={credential.revokedAt ? 'red' : 'gray'}>{credential.type}</Badge>
            </Table.Td>
            <Table.Td>{formatDate(credential.expiresAt)}</Table.Td>
            <Table.Td>{formatDate(credential.lastUsedAt)}</Table.Td>
            <Table.Td>
              {!credential.revokedAt && (
                <Button
                  size="xs"
                  variant="subtle"
                  color="red"
                  aria-label={`Revoke ${credential.description ?? credential.id}`}
                  loading={revokeCredential.isPending}
                  onClick={() => revokeCredential.mutate(credential.id)}
                >
                  Revoke
                </Button>
              )}
            </Table.Td>
          </Table.Tr>
        ))}
      </Table.Tbody>
    </Table>
  );
}

function AddSecretModal({ applicationId, opened, onClose }: { applicationId: string; opened: boolean; onClose: () => void }) {
  const addSecret = useAddApplicationSecret(applicationId);
  const [description, setDescription] = useState('');
  const [expiresAt, setExpiresAt] = useState('');
  const [revokeExisting, setRevokeExisting] = useState(false);
  const [clientSecret, setClientSecret] = useState<string | null>(null);

  const resetAndClose = () => {
    setDescription('');
    setExpiresAt('');
    setRevokeExisting(false);
    setClientSecret(null);
    onClose();
  };

  return (
    <Modal opened={opened} onClose={resetAndClose} title="Add secret" centered>
      {clientSecret ? (
        <Stack>
          <SecretDisplay label="Client secret" value={clientSecret} />
          <Group justify="flex-end">
            <Button onClick={resetAndClose}>Done</Button>
          </Group>
        </Stack>
      ) : (
        <Stack>
          {addSecret.isError && <Alert color="red">{getApiErrorMessage(addSecret.error)}</Alert>}
          <TextInput label="Description" value={description} onChange={(event) => setDescription(event.currentTarget.value)} />
          <TextInput label="Expires at" type="datetime-local" value={expiresAt} onChange={(event) => setExpiresAt(event.currentTarget.value)} />
          <Checkbox
            label="Revoke existing secrets"
            checked={revokeExisting}
            onChange={(event) => setRevokeExisting(event.currentTarget.checked)}
          />
          <Group justify="flex-end">
            <Button variant="default" onClick={resetAndClose}>Cancel</Button>
            <Button
              loading={addSecret.isPending}
              onClick={async () => {
                const response = await addSecret.mutateAsync({
                  description: description || null,
                  expiresAt: expiresAt || null,
                  revokeExisting,
                });
                setClientSecret(response.clientSecret);
              }}
            >
              Add secret
            </Button>
          </Group>
        </Stack>
      )}
    </Modal>
  );
}

function AddCertificateModal({ applicationId, opened, onClose }: { applicationId: string; opened: boolean; onClose: () => void }) {
  const addCertificate = useAddApplicationCertificate(applicationId);
  const [thumbprint, setThumbprint] = useState('');
  const [subject, setSubject] = useState('');
  const [description, setDescription] = useState('');
  const [expiresAt, setExpiresAt] = useState('');

  const resetAndClose = () => {
    setThumbprint('');
    setSubject('');
    setDescription('');
    setExpiresAt('');
    onClose();
  };

  return (
    <Modal opened={opened} onClose={resetAndClose} title="Add certificate" centered>
      <Stack>
        {addCertificate.isError && <Alert color="red">{getApiErrorMessage(addCertificate.error)}</Alert>}
        <TextInput label="Thumbprint" value={thumbprint} onChange={(event) => setThumbprint(event.currentTarget.value)} />
        <TextInput label="Subject" value={subject} onChange={(event) => setSubject(event.currentTarget.value)} />
        <TextInput label="Description" value={description} onChange={(event) => setDescription(event.currentTarget.value)} />
        <TextInput label="Expires at" type="datetime-local" value={expiresAt} onChange={(event) => setExpiresAt(event.currentTarget.value)} />
        <Group justify="flex-end">
          <Button variant="default" onClick={resetAndClose}>Cancel</Button>
          <Button
            loading={addCertificate.isPending}
            onClick={async () => {
              await addCertificate.mutateAsync({
                thumbprint,
                subject: subject || null,
                description: description || null,
                expiresAt: expiresAt || null,
              });
              resetAndClose();
            }}
          >
            Add certificate
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString() : 'N/A';
}
