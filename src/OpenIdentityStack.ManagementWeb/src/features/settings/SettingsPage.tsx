import { Alert, Badge, Checkbox, Group, NativeSelect, Paper, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { useState } from 'react';
import { getApiErrorMessage } from '@/lib/admin-api';
import { LoadingState, PageHeader } from '@/components/PagePrimitives';
import {
  useAuthenticationProviders,
  useAuthenticationSettings,
  useSetDefaultProvider,
  useSetLocalFallback,
} from './settings-hooks';

export function SettingsPage() {
  const settings = useAuthenticationSettings();
  const providers = useAuthenticationProviders();
  const setDefaultProviderMutation = useSetDefaultProvider();
  const setLocalFallbackMutation = useSetLocalFallback();
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const isLoading = settings.isLoading || providers.isLoading;
  const error = settings.error ?? providers.error;

  if (isLoading) {
    return <LoadingState title="Loading settings" description="Fetching authentication configuration..." />;
  }

  if (settings.isError || providers.isError) {
    return <Alert color="red">Failed to load authentication settings. {getApiErrorMessage(error)}</Alert>;
  }

  if (!settings.data) {
    return <Alert color="red">Authentication settings not found.</Alert>;
  }

  const activeProviders = (providers.data ?? []).filter((provider) => provider.isActive);
  const isExternalProviderDefault = !settings.data.isLocalDefault;

  async function handleDefaultProviderChange(providerId: string) {
    setSuccessMessage(null);
    setErrorMessage(null);

    try {
      await setDefaultProviderMutation.mutateAsync({ providerId });
      setSuccessMessage('Default authentication provider updated successfully.');
    } catch (unknownError) {
      setErrorMessage(getApiErrorMessage(unknownError));
    }
  }

  async function handleLocalFallbackChange(enabled: boolean) {
    setSuccessMessage(null);
    setErrorMessage(null);

    try {
      await setLocalFallbackMutation.mutateAsync({ enabled });
      setSuccessMessage('Local fallback setting updated successfully.');
    } catch (unknownError) {
      setErrorMessage(getApiErrorMessage(unknownError));
    }
  }

  return (
    <Stack gap="lg">
      <PageHeader
        title="Authentication Settings"
        description="Configure default authentication provider and local fallback options."
      />

      {successMessage && <Alert color="green">{successMessage}</Alert>}
      {errorMessage && <Alert color="red">{errorMessage}</Alert>}

      <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
        <Paper withBorder p="md">
          <Stack gap="md">
            <div>
              <Title order={2} size="h3">Default Authentication Provider</Title>
              <Text c="dimmed" size="sm">
                Select which authentication method is presented as the primary login option.
              </Text>
            </div>

            <NativeSelect
              label="Default Provider"
              value={settings.data.defaultProviderId}
              disabled={setDefaultProviderMutation.isPending}
              data={activeProviders.map((provider) => ({
                value: provider.id,
                label: `${provider.displayName} (${provider.type})`,
              }))}
              onChange={(event) => void handleDefaultProviderChange(event.currentTarget.value)}
            />

            <Group gap="xs">
              <Text size="sm">Current status:</Text>
              {settings.data.isLocalDefault ? (
                <Badge color="gray" variant="light">Local Accounts (Default)</Badge>
              ) : (
                <Badge color="blue" variant="light">External Provider</Badge>
              )}
            </Group>
          </Stack>
        </Paper>

        <Paper withBorder p="md">
          <Stack gap="md">
            <div>
              <Title order={2} size="h3">Admin Local Fallback</Title>
              <Text c="dimmed" size="sm">
                Allows IAM administrators to use local credentials when an external provider is default.
              </Text>
            </div>

            <Checkbox
              label="Enable local fallback for IAM admins"
              checked={settings.data.localFallbackEnabled}
              disabled={setLocalFallbackMutation.isPending || settings.data.isLocalDefault}
              onChange={(event) => void handleLocalFallbackChange(event.currentTarget.checked)}
            />

            {settings.data.isLocalDefault && (
              <Alert color="blue">
                Local fallback is not applicable while Local Accounts is the default provider.
              </Alert>
            )}

            {isExternalProviderDefault && (
              <Alert color="yellow">
                When an external provider is default, local account authentication is limited to IAM administrators
                {settings.data.localFallbackEnabled ? ' and currently enabled.' : ' and currently disabled.'}
              </Alert>
            )}
          </Stack>
        </Paper>
      </SimpleGrid>

    </Stack>
  );
}
