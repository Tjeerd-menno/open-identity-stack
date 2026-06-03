import { Alert, Badge, Checkbox, Group, NativeSelect, Paper, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { useState } from 'react';
import { getApiErrorMessage } from '@/lib/admin-api';
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
    return <Text>Loading authentication settings</Text>;
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
      <div>
        <Title order={1}>Authentication Settings</Title>
        <Text c="dimmed">Configure default authentication provider and local fallback options.</Text>
      </div>

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

      <section aria-label="Authentication provider selection guidance">
        <Title order={2} size="h3">How Authentication Provider Selection Works</Title>
        <Stack gap="sm" mt="sm">
          <div>
            <Title order={3} size="h4">Default Provider Selection</Title>
            <Text size="sm" c="dimmed">
              The default provider determines which login method is presented first on the authentication page.
            </Text>
          </div>
          <div>
            <Title order={3} size="h4">Admin-Only Local Fallback</Title>
            <Text size="sm" c="dimmed">
              IAM administrators retain local credentials for emergency access when an external provider is primary.
            </Text>
          </div>
          <div>
            <Title order={3} size="h4">Login Page Behavior</Title>
            <Text size="sm" c="dimmed">
              Local Accounts shows the username/password form first; external providers make provider sign-in primary.
            </Text>
          </div>
        </Stack>
      </section>

      {settings.data.updatedAt && (
        <Text size="sm" c="dimmed" ta="right">
          Last updated: {new Date(settings.data.updatedAt).toLocaleString()}
        </Text>
      )}
    </Stack>
  );
}
