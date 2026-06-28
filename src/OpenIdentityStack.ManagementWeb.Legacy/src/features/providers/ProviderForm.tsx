import { Alert, Button, Checkbox, Group, PasswordInput, Stack, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { getApiErrorMessage } from '@/lib/admin-api';
import { firstError, maxLength, required, validHttpUrl, validSlug } from '@/lib/form-validation';
import type { CreateProviderRequest, Provider, UpdateProviderRequest } from './providers-api';

type ProviderFormValues = {
  name: string;
  displayName: string;
  authority: string;
  clientId: string;
  clientSecret: string;
  scopes: string;
  jitProvisioningEnabled: boolean;
};

type ProviderFormProps = {
  provider?: Provider;
  mode: 'create' | 'edit';
  loading?: boolean;
  error?: unknown;
  onSubmit: (data: CreateProviderRequest | UpdateProviderRequest) => Promise<void> | void;
  onCancel: () => void;
};

export function ProviderForm({ provider, mode, loading = false, error, onSubmit, onCancel }: ProviderFormProps) {
  const form = useForm<ProviderFormValues>({
    mode: 'controlled',
    validateInputOnBlur: true,
    initialValues: {
      name: provider?.name ?? '',
      displayName: provider?.displayName ?? '',
      authority: provider?.authority ?? '',
      clientId: provider?.clientId ?? '',
      clientSecret: '',
      scopes: provider?.scopes.join(' ') ?? 'openid profile email',
      jitProvisioningEnabled: provider?.jitProvisioningEnabled ?? true,
    },
    validate: {
      name: (value) => mode === 'create'
        ? firstError(
          required(value, 'Provider name is required.'),
          validSlug(value, 'Provider name'),
          maxLength(value, 100, 'Provider name')
        )
        : null,
      displayName: (value) => firstError(
        required(value, 'Display name is required.'),
        maxLength(value, 200, 'Display name')
      ),
      authority: (value) => mode === 'create'
        ? firstError(
          required(value, 'Authority is required.'),
          validHttpUrl(value, 'Authority must be a valid HTTP or HTTPS URL.')
        )
        : null,
      clientId: (value) => firstError(
        required(value, 'Client ID is required.'),
        maxLength(value, 256, 'Client ID')
      ),
      clientSecret: (value) => maxLength(value, 4096, 'Client secret'),
      scopes: (value) => firstError(
        splitScopes(value).length === 0 ? 'At least one scope is required.' : null,
        maxLength(value, 1000, 'Scopes')
      ),
    },
  });
  const errorMessage = error ? getApiErrorMessage(error) : null;

  async function handleSubmit(values: ProviderFormValues) {
    const common = {
      displayName: values.displayName.trim(),
      clientId: values.clientId.trim(),
      clientSecret: values.clientSecret.trim() || undefined,
      scopes: splitScopes(values.scopes),
      jitProvisioningEnabled: values.jitProvisioningEnabled,
    };

    if (mode === 'create') {
      await onSubmit({
        name: values.name.trim(),
        authority: values.authority.trim(),
        ...common,
      });
      return;
    }

    await onSubmit(common);
  }

  return (
    <form noValidate onSubmit={form.onSubmit((values) => void handleSubmit(values))}>
      <Stack gap="md">
        {errorMessage && <Alert color="red">{errorMessage}</Alert>}

        <TextInput
          label="Provider name"
          disabled={mode === 'edit'}
          required
          {...form.getInputProps('name')}
        />
        <TextInput
          label="Display name"
          required
          {...form.getInputProps('displayName')}
        />
        <TextInput
          label="Authority"
          disabled={mode === 'edit'}
          required
          {...form.getInputProps('authority')}
        />
        <TextInput
          label="Client ID"
          required
          {...form.getInputProps('clientId')}
        />
        <PasswordInput
          label="Client secret"
          {...form.getInputProps('clientSecret')}
        />
        <TextInput
          label="Scopes"
          required
          {...form.getInputProps('scopes')}
        />
        <Checkbox
          label="JIT provisioning"
          {...form.getInputProps('jitProvisioningEnabled', { type: 'checkbox' })}
        />

        <Group justify="flex-end">
          <Button type="button" variant="default" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" loading={loading}>
            {mode === 'create' ? 'Create provider' : 'Save provider'}
          </Button>
        </Group>
      </Stack>
    </form>
  );
}

function splitScopes(scopes: string) {
  return scopes.split(/\s+/).map((scope) => scope.trim()).filter(Boolean);
}
