import { Alert, Button, Checkbox, Group, PasswordInput, Stack, TextInput } from '@mantine/core';
import { FormEvent, useState } from 'react';
import { getApiErrorMessage } from '@/lib/admin-api';
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
  const [values, setValues] = useState<ProviderFormValues>({
    name: provider?.name ?? '',
    displayName: provider?.displayName ?? '',
    authority: provider?.authority ?? '',
    clientId: provider?.clientId ?? '',
    clientSecret: '',
    scopes: provider?.scopes.join(' ') ?? 'openid profile email',
    jitProvisioningEnabled: provider?.jitProvisioningEnabled ?? true,
  });
  const [validationError, setValidationError] = useState<string | null>(null);
  const errorMessage = validationError ?? (error ? getApiErrorMessage(error) : null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const message = validate(values, mode);

    if (message) {
      setValidationError(message);
      return;
    }

    setValidationError(null);

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
    <form onSubmit={(event) => void handleSubmit(event)}>
      <Stack gap="md">
        {errorMessage && <Alert color="red">{errorMessage}</Alert>}

        <TextInput
          label="Provider name"
          value={values.name}
          disabled={mode === 'edit'}
          onChange={(event) => setValues({ ...values, name: event.currentTarget.value })}
          required
        />
        <TextInput
          label="Display name"
          value={values.displayName}
          onChange={(event) => setValues({ ...values, displayName: event.currentTarget.value })}
          required
        />
        <TextInput
          label="Authority"
          value={values.authority}
          disabled={mode === 'edit'}
          onChange={(event) => setValues({ ...values, authority: event.currentTarget.value })}
          required
        />
        <TextInput
          label="Client ID"
          value={values.clientId}
          onChange={(event) => setValues({ ...values, clientId: event.currentTarget.value })}
          required
        />
        <PasswordInput
          label="Client secret"
          value={values.clientSecret}
          onChange={(event) => setValues({ ...values, clientSecret: event.currentTarget.value })}
        />
        <TextInput
          label="Scopes"
          value={values.scopes}
          onChange={(event) => setValues({ ...values, scopes: event.currentTarget.value })}
          required
        />
        <Checkbox
          label="JIT provisioning"
          checked={values.jitProvisioningEnabled}
          onChange={(event) => setValues({ ...values, jitProvisioningEnabled: event.currentTarget.checked })}
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

function validate(values: ProviderFormValues, mode: 'create' | 'edit') {
  if (mode === 'create' && !/^[a-z0-9-]+$/.test(values.name.trim())) {
    return 'Provider name must contain lowercase letters, numbers, or hyphens.';
  }

  if (values.displayName.trim().length === 0) {
    return 'Display name is required.';
  }

  try {
    new URL(values.authority.trim());
  } catch {
    return 'Authority must be a valid URL.';
  }

  if (values.clientId.trim().length === 0) {
    return 'Client ID is required.';
  }

  if (splitScopes(values.scopes).length === 0) {
    return 'At least one scope is required.';
  }

  return null;
}

function splitScopes(scopes: string) {
  return scopes.split(/\s+/).map((scope) => scope.trim()).filter(Boolean);
}
