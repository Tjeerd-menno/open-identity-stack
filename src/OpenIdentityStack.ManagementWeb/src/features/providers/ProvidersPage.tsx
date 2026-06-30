import { Box, Button, Group, Modal, Stack, Switch, Text, TextInput, ThemeIcon } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import type { Provider } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { DataTable, type Column } from '@/components/DataTable';
import { RowMenu } from '@/components/RowMenu';
import { ErrorState, PageHeader, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';

export function ProvidersPage() {
  const auth = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(auth.permissions, 'providers:write');
  const [createOpened, createControls] = useDisclosure(false);

  const query = useQuery({ queryKey: ['providers'], queryFn: () => api.providers.getProviders(true) });

  const toggle = useMutation({
    mutationFn: (provider: Provider) =>
      provider.status === 'Disabled' ? api.providers.enableProvider(provider.id) : api.providers.disableProvider(provider.id),
    onSuccess: () => {
      notifications.show({ message: 'Provider updated', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['providers'] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const columns: Column<Provider>[] = [
    {
      key: 'provider',
      header: 'Provider',
      render: (provider) => (
        <Group gap="sm" wrap="nowrap">
          <ThemeIcon color="blue" radius="md" size={36} variant="light">
            <Icon name="globe" size={18} />
          </ThemeIcon>
          <Box style={{ minWidth: 0 }}>
            <Text fw={600} size="sm">
              {provider.displayName || provider.name}
            </Text>
            <Text c="dimmed" className="mw-mono" size="xs" truncate>
              {provider.authority}
            </Text>
          </Box>
        </Group>
      ),
    },
    {
      key: 'jit',
      header: 'JIT provisioning',
      render: (provider) => (
        <Text c="dimmed" size="sm">
          {provider.jitProvisioningEnabled ? 'Enabled' : 'Disabled'}
        </Text>
      ),
    },
    { key: 'status', header: 'Status', render: (provider) => <StatusBadge status={provider.status} /> },
    {
      key: 'actions',
      header: '',
      align: 'right',
      width: 48,
      render: (provider) => (
        <RowMenu
          items={[
            { label: 'Open', icon: 'arrow-up-right', onClick: () => navigate(`/providers/${provider.id}`) },
            {
              label: 'Copy provider ID',
              icon: 'copy',
              onClick: () => {
                void navigator.clipboard?.writeText(provider.id);
                notifications.show({ message: 'Provider ID copied', color: 'gray' });
              },
            },
            { separator: true },
            {
              label: provider.status === 'Disabled' ? 'Enable provider' : 'Disable provider',
              icon: 'power',
              disabled: !canWrite,
              onClick: () => toggle.mutate(provider),
            },
          ]}
        />
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Identity providers"
        description="Upstream OIDC/OAuth providers that can sign your users in."
        actions={
          canWrite ? (
            <Button leftSection={<Icon name="plus" size={16} />} onClick={createControls.open}>
              Add provider
            </Button>
          ) : undefined
        }
      />

      {query.isError ? (
        <ErrorState message={getApiErrorMessage(query.error)} />
      ) : (
        <DataTable
          columns={columns}
          rows={query.data ?? []}
          getRowKey={(provider) => provider.id}
          onRowClick={(provider) => navigate(`/providers/${provider.id}`)}
          isLoading={query.isLoading}
          emptyIcon="globe"
          emptyTitle="No identity providers"
          emptyText="Add an upstream provider to let users sign in with it."
        />
      )}

      {createOpened && <CreateProviderModal onClose={createControls.close} />}
    </div>
  );
}

function CreateProviderModal({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient();
  const form = useForm({
    initialValues: { name: '', displayName: '', authority: '', clientId: '', clientSecret: '', scopes: 'openid, email, profile', jit: true },
    validate: {
      name: (value) => (value.trim() ? null : 'Required'),
      authority: (value) => (/^https?:\/\//.test(value) ? null : 'Enter a valid issuer URL'),
      clientId: (value) => (value.trim() ? null : 'Required'),
    },
  });

  const mutation = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.providers.createProvider({
        name: values.name,
        displayName: values.displayName || undefined,
        authority: values.authority,
        clientId: values.clientId,
        clientSecret: values.clientSecret || undefined,
        scopes: values.scopes.split(',').map((scope) => scope.trim()).filter(Boolean),
        jitProvisioningEnabled: values.jit,
      }),
    onSuccess: (provider) => {
      notifications.show({ message: `Added ${provider.displayName || provider.name}`, color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['providers'] });
      onClose();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <Modal opened onClose={onClose} title="Add identity provider" centered size="lg">
      <form onSubmit={form.onSubmit((values) => mutation.mutate(values))}>
        <Stack gap="md">
          <TextInput label="Name" placeholder="google-workspace" required {...form.getInputProps('name')} />
          <TextInput label="Display name" placeholder="Google Workspace" {...form.getInputProps('displayName')} />
          <TextInput label="Issuer / authority" placeholder="https://accounts.google.com" required {...form.getInputProps('authority')} />
          <TextInput label="Client ID" required {...form.getInputProps('clientId')} />
          <TextInput label="Client secret" {...form.getInputProps('clientSecret')} />
          <TextInput label="Scopes" description="Comma-separated" {...form.getInputProps('scopes')} />
          <Switch label="Enable just-in-time provisioning" {...form.getInputProps('jit', { type: 'checkbox' })} />
          <Group justify="flex-end" gap="sm" mt="xs">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={mutation.isPending}>
              Add provider
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
