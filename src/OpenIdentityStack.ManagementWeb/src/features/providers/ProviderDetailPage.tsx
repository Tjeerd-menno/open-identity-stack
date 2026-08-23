import { Badge, Button, Group, PasswordInput, Stack, Switch, Tabs, Text, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useDisclosure } from '@mantine/hooks';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router';
import type { Provider } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { ConfirmModal } from '@/components/ConfirmModal';
import { BackLink, CenteredState, DetailHeader, ErrorState, FieldRow, MetaStrip, SectionCard, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import { formatDateTime } from '@/lib/format';
import { useSyncedForm } from '@/lib/use-synced-form';

export function ProviderDetailPage() {
  const { providerId = '' } = useParams();
  const navigate = useNavigate();
  const auth = useAuth();
  const queryClient = useQueryClient();
  const canWrite = hasPermission(auth.permissions, 'providers:write');
  // Deletion is authorized with providers:delete, independent of providers:write.
  const canDelete = hasPermission(auth.permissions, 'providers:delete');
  const [confirmDeleteOpened, confirmDeleteControls] = useDisclosure(false);

  const providerQuery = useQuery({ queryKey: ['provider', providerId], queryFn: () => api.providers.getProvider(providerId) });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['provider', providerId] });
    void queryClient.invalidateQueries({ queryKey: ['providers'] });
  };

  const toggle = useMutation({
    mutationFn: (provider: Provider) =>
      provider.status === 'Disabled' ? api.providers.enableProvider(provider.id) : api.providers.disableProvider(provider.id),
    onSuccess: () => {
      notifications.show({ message: 'Provider updated', color: 'green' });
      invalidate();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const setJit = useMutation({
    mutationFn: (enabled: boolean) => api.providers.updateProvider(providerId, { jitProvisioningEnabled: enabled }),
    onSuccess: () => {
      notifications.show({ message: 'Provider updated', color: 'green' });
      invalidate();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const remove = useMutation({
    mutationFn: () => api.providers.deleteProvider(providerId),
    onSuccess: () => {
      notifications.show({ message: 'Provider deleted', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['providers'] });
      navigate('/providers');
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  if (providerQuery.isLoading) {
    return <CenteredState loading title="Loading provider…" />;
  }
  if (providerQuery.isError || !providerQuery.data) {
    return <ErrorState message={getApiErrorMessage(providerQuery.error)} />;
  }

  const provider = providerQuery.data;

  return (
    <div>
      <BackLink label="Back to identity providers" to="/providers" />
      <DetailHeader
        icon="globe"
        title={provider.displayName || provider.name}
        description={provider.authority}
        badge={<StatusBadge status={provider.status} />}
        actions={
          canWrite ? (
            <Button
              variant="default"
              loading={toggle.isPending}
              leftSection={<Icon name={provider.status === 'Disabled' ? 'power' : 'ban'} size={16} />}
              onClick={() => toggle.mutate(provider)}
            >
              {provider.status === 'Disabled' ? 'Enable provider' : 'Disable provider'}
            </Button>
          ) : undefined
        }
      />

      <MetaStrip
        items={[
          { label: 'Status', value: provider.status },
          { label: 'JIT provisioning', value: provider.jitProvisioningEnabled ? 'On' : 'Off' },
          { label: 'Scopes', value: provider.scopes.length },
          { label: 'Created', value: formatDateTime(provider.createdAt) },
        ]}
      />

      <Tabs defaultValue="connection" color="blue" keepMounted={false}>
        <Tabs.List mb="lg">
          <Tabs.Tab value="connection">Connection</Tabs.Tab>
          <Tabs.Tab value="settings">Settings</Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="connection">
          <Stack gap="lg">
            <SectionCard title="Connection">
              <FieldRow label="Name" value={provider.name} mono />
              <FieldRow label="Issuer / authority" value={provider.authority} mono />
              <FieldRow label="Client ID" value={provider.clientId} mono />
              <FieldRow label="Created" value={formatDateTime(provider.createdAt)} last />
            </SectionCard>
            <SectionCard title="Scopes" description="Scopes requested from this provider during sign-in.">
              <Group gap="xs">
                {provider.scopes.length === 0 ? (
                  <Text c="dimmed" size="sm">
                    No scopes configured.
                  </Text>
                ) : (
                  provider.scopes.map((scope) => (
                    <Badge key={scope} color="blue" variant="light" style={{ fontFamily: 'var(--mw-mono)' }}>
                      {scope}
                    </Badge>
                  ))
                )}
              </Group>
            </SectionCard>
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="settings">
          <Stack gap="lg">
            <ProviderConfigForm provider={provider} canWrite={canWrite} onSaved={invalidate} />

            <SectionCard title="Provisioning">
              <Group justify="space-between" wrap="nowrap">
                <div>
                  <Text fw={600} size="sm">
                    Just-in-time provisioning
                  </Text>
                  <Text c="dimmed" size="xs">
                    Create a local account automatically the first time a user signs in with this provider.
                  </Text>
                </div>
                <Switch
                  aria-label="Just-in-time provisioning"
                  checked={provider.jitProvisioningEnabled}
                  disabled={!canWrite || setJit.isPending}
                  onChange={(event) => setJit.mutate(event.currentTarget.checked)}
                />
              </Group>
            </SectionCard>

            {canDelete && (
              <SectionCard title="Danger zone" description="Deleting a provider prevents its users from signing in with it." danger>
                <Button color="red" variant="light" leftSection={<Icon name="trash-2" size={16} />} onClick={confirmDeleteControls.open}>
                  Delete provider
                </Button>
              </SectionCard>
            )}
          </Stack>
        </Tabs.Panel>
      </Tabs>

      <ConfirmModal
        opened={confirmDeleteOpened}
        title="Delete provider"
        message={`Permanently delete ${provider.displayName || provider.name}? Users linked to this provider will no longer be able to sign in with it.`}
        confirmLabel="Delete provider"
        loading={remove.isPending}
        onConfirm={() => remove.mutate()}
        onClose={confirmDeleteControls.close}
      />
    </div>
  );
}

function ProviderConfigForm({ provider, canWrite, onSaved }: { provider: Provider; canWrite: boolean; onSaved: () => void }) {
  const form = useForm({
    initialValues: {
      displayName: provider.displayName ?? '',
      clientId: provider.clientId ?? '',
      clientSecret: '',
      scopes: provider.scopes.join(', '),
    },
    validate: { clientId: (value) => (value.trim() ? null : 'Required') },
  });

  useSyncedForm(
    form,
    {
      displayName: provider.displayName ?? '',
      clientId: provider.clientId ?? '',
      clientSecret: '',
      scopes: provider.scopes.join(', '),
    },
    provider.id
  );

  const save = useMutation({
    mutationFn: (values: typeof form.values) =>
      api.providers.updateProvider(provider.id, {
        displayName: values.displayName,
        clientId: values.clientId,
        // Only send a secret when the operator typed one — leaving it blank keeps the stored value.
        clientSecret: values.clientSecret ? values.clientSecret : undefined,
        scopes: values.scopes.split(',').map((scope) => scope.trim()).filter(Boolean),
      }),
    onSuccess: () => {
      notifications.show({ message: 'Provider updated', color: 'green' });
      form.setFieldValue('clientSecret', '');
      onSaved();
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  return (
    <SectionCard title="Configuration" description="Connection details used when signing users in with this provider.">
      <form onSubmit={form.onSubmit((values) => save.mutate(values))}>
        <Stack gap="md">
          <TextInput label="Display name" disabled={!canWrite} {...form.getInputProps('displayName')} />
          <TextInput label="Client ID" disabled={!canWrite} {...form.getInputProps('clientId')} />
          <PasswordInput
            label="Client secret"
            description="Leave blank to keep the current secret."
            placeholder="••••••••"
            disabled={!canWrite}
            {...form.getInputProps('clientSecret')}
          />
          <TextInput label="Scopes" description="Comma-separated" disabled={!canWrite} {...form.getInputProps('scopes')} />
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
