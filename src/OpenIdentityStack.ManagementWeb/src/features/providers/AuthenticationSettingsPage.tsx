import { Group, Select, Stack, Switch, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CenteredState, ErrorState, FieldRow, PageHeader, SectionCard } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { formatDateTime } from '@/lib/format';

export function AuthenticationSettingsPage() {
  const queryClient = useQueryClient();
  const settingsQuery = useQuery({
    queryKey: ['authentication-settings'],
    queryFn: () => api.settings.getAuthenticationSettings(),
  });
  const providersQuery = useQuery({
    queryKey: ['authentication-settings', 'providers'],
    queryFn: () => api.settings.getAuthenticationProviders(),
  });

  const setDefault = useMutation({
    mutationFn: (providerId: string) => api.settings.setDefaultProvider({ providerId }),
    onSuccess: () => {
      notifications.show({ message: 'Default provider updated', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['authentication-settings'] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  const setFallback = useMutation({
    mutationFn: (enabled: boolean) => api.settings.setLocalFallback({ enabled }),
    onSuccess: () => {
      notifications.show({ message: 'Local fallback updated', color: 'green' });
      void queryClient.invalidateQueries({ queryKey: ['authentication-settings'] });
    },
    onError: (error) => notifications.show({ message: getApiErrorMessage(error), color: 'red' }),
  });

  if (settingsQuery.isLoading) {
    return <CenteredState loading title="Loading settings…" />;
  }
  if (settingsQuery.isError || !settingsQuery.data) {
    return <ErrorState message={getApiErrorMessage(settingsQuery.error)} />;
  }

  const settings = settingsQuery.data;

  return (
    <div>
      <PageHeader
        title="Authentication settings"
        description="Control the default sign-in method and whether local password sign-in is available as a fallback."
      />

      <Stack gap="lg">
        <SectionCard title="Default sign-in">
          <Stack gap="md">
            <Select
              label="Default provider"
              description="The provider users see first on the sign-in screen."
              data={(providersQuery.data ?? []).map((provider) => ({ value: provider.id, label: provider.displayName }))}
              value={settings.defaultProviderId}
              onChange={(value) => value && setDefault.mutate(value)}
              disabled={setDefault.isPending}
            />
            <Group justify="space-between" wrap="nowrap">
              <div>
                <Text fw={600} size="sm">
                  Local password fallback
                </Text>
                <Text c="dimmed" size="xs">
                  Allow operators to sign in with a local account if upstream sign-in is unavailable.
                </Text>
              </div>
              <Switch
                aria-label="Local password fallback"
                checked={settings.localFallbackEnabled}
                onChange={(event) => setFallback.mutate(event.currentTarget.checked)}
                disabled={setFallback.isPending}
              />
            </Group>
          </Stack>
        </SectionCard>

        <SectionCard title="Current configuration">
          <FieldRow label="Local is default" value={settings.isLocalDefault ? 'Yes' : 'No'} />
          <FieldRow label="Local fallback" value={settings.localFallbackEnabled ? 'Enabled' : 'Disabled'} />
          <FieldRow label="Last updated" value={formatDateTime(settings.updatedAt)} last />
        </SectionCard>
      </Stack>
    </div>
  );
}
