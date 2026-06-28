import { Alert, Box, Button, Checkbox, Code, CopyButton, Group, Modal, Select, Stack, Text, TextInput } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import type { ApplicationCreatedResponse, ApplicationProfile } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { UriList } from '@/components/UriList';
import { api, getApiErrorMessage } from '@/lib/api';

const PROFILE_LABELS: Record<string, string> = {
  Web: 'Web',
  SinglePage: 'Single page',
  Native: 'Native',
  MachineToMachine: 'Machine-to-machine',
  Device: 'Device',
  Custom: 'Custom',
};

export function CreateApplicationModal({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient();
  const [created, setCreated] = useState<ApplicationCreatedResponse | null>(null);

  const policiesQuery = useQuery({
    queryKey: ['application-profile-policies'],
    queryFn: () => api.applications.getApplicationProfilePolicies(),
  });

  const form = useForm({
    initialValues: {
      displayName: '',
      clientId: '',
      profile: 'Web' as ApplicationProfile,
      redirectUris: [''],
      postLogoutRedirectUris: [''],
      allowRefreshTokens: true,
    },
    validate: {
      displayName: (value) => (value.trim() ? null : 'Required'),
      clientId: (value) => (value.trim() ? null : 'Required'),
    },
  });

  const policy = useMemo(
    () => (policiesQuery.data ?? []).find((item) => item.applicationProfile === form.values.profile),
    [policiesQuery.data, form.values.profile]
  );
  const policies = policiesQuery.data ?? [];
  const requiresRedirect = policy?.requiresRedirectUris ?? false;

  const mutation = useMutation({
    mutationFn: () => {
      const redirectUris = form.values.redirectUris.map((uri) => uri.trim()).filter(Boolean);
      const postLogoutRedirectUris = form.values.postLogoutRedirectUris.map((uri) => uri.trim()).filter(Boolean);
      const grants = new Set(policy?.defaultGrantTypes ?? ['authorization_code']);
      const scopes = new Set(['openid', 'profile', 'email']);
      if (form.values.allowRefreshTokens && grants.has('authorization_code')) {
        grants.add('refresh_token');
        scopes.add('offline_access');
      }
      return api.applications.createApplication({
        clientId: form.values.clientId.trim(),
        displayName: form.values.displayName.trim(),
        profile: form.values.profile,
        clientType: policy?.defaultClientProfile ?? 'Confidential',
        allowedGrantTypes: [...grants],
        allowedScopes: [...scopes],
        redirectUris,
        postLogoutRedirectUris,
        requirePkce: policy?.defaultRequirePkce ?? false,
        requireConsent: policy?.defaultRequireConsent ?? false,
      });
    },
    onSuccess: (response) => {
      void queryClient.invalidateQueries({ queryKey: ['applications'] });
      if (response.initialSecret) {
        setCreated(response);
      } else {
        onClose();
      }
    },
  });

  const submit = form.onSubmit(() => {
    if (requiresRedirect && form.values.redirectUris.every((uri) => !uri.trim())) {
      form.setFieldError('redirectUris', 'At least one redirect URI is required for this profile.');
      return;
    }
    mutation.mutate();
  });

  if (created) {
    return (
      <Modal opened onClose={onClose} title="Application created" centered>
        <Stack gap="md">
          <Alert color="green" icon={<Icon name="circle-check" size={18} />} variant="light">
            {created.displayName} was registered.
          </Alert>
          {created.initialSecret && (
            <Box>
              <Text fw={600} size="sm">
                Client secret
              </Text>
              <Text c="dimmed" size="xs" mb={6}>
                Copy it now — this secret won't be shown again.
              </Text>
              <Group gap="xs" wrap="nowrap">
                <Code style={{ flex: 1, wordBreak: 'break-all', padding: 8 }}>{created.initialSecret}</Code>
                <CopyButton value={created.initialSecret}>
                  {({ copied, copy }) => (
                    <Button variant="light" color={copied ? 'green' : 'blue'} leftSection={<Icon name={copied ? 'check' : 'copy'} size={15} />} onClick={copy}>
                      {copied ? 'Copied' : 'Copy'}
                    </Button>
                  )}
                </CopyButton>
              </Group>
            </Box>
          )}
          <Group justify="flex-end">
            <Button onClick={onClose}>Done</Button>
          </Group>
        </Stack>
      </Modal>
    );
  }

  return (
    <Modal opened onClose={onClose} title="Create application" centered size="lg">
      <form onSubmit={submit}>
        <Stack gap="md">
          {mutation.isError && (
            <Alert color="red" icon={<Icon name="circle-alert" size={18} />} variant="light">
              {getApiErrorMessage(mutation.error)}
            </Alert>
          )}
          <TextInput label="Display name" placeholder="Northwind Web" required {...form.getInputProps('displayName')} />
          <TextInput
            label="Client ID"
            description="Used in OAuth token requests. Keep it stable."
            placeholder="northwind-web"
            required
            {...form.getInputProps('clientId')}
          />
          <Select
            label="Application profile"
            description="Fixes the client type, grant types and redirect requirements."
            required
            allowDeselect={false}
            data={policies
              .filter((item) => item.isSelectable)
              .map((item) => ({ value: item.applicationProfile, label: PROFILE_LABELS[item.applicationProfile] ?? item.applicationProfile }))}
            value={form.values.profile}
            onChange={(value) => value && form.setFieldValue('profile', value as ApplicationProfile)}
          />

          {requiresRedirect ? (
            <>
              <UriList
                label="Redirect URIs *"
                description="Where the authorization server returns the user after sign-in."
                values={form.values.redirectUris}
                onChange={(values) => form.setFieldValue('redirectUris', values)}
                placeholder="https://app.example.com/callback"
                error={form.errors.redirectUris as string | undefined}
              />
              <UriList
                label="Post-logout redirect URIs"
                description="Optional. Where users may be sent after signing out."
                values={form.values.postLogoutRedirectUris}
                onChange={(values) => form.setFieldValue('postLogoutRedirectUris', values)}
                placeholder="https://app.example.com/signed-out"
              />
            </>
          ) : (
            <Alert color="blue" icon={<Icon name="server" size={18} />} variant="light">
              Machine-to-machine clients use <code style={{ fontFamily: 'var(--mw-mono)' }}>client_credentials</code> and never redirect a user, so no redirect URIs are needed.
            </Alert>
          )}

          {policy && (
            <Box p="md" style={{ background: 'var(--mw-surface-sunken)', border: '1px solid var(--mw-border)', borderRadius: 'var(--mantine-radius-md)' }}>
              <Text c="dimmed" fw={600} mb="xs" size="xs" tt="uppercase" style={{ letterSpacing: '0.04em' }}>
                Profile defaults
              </Text>
              <Stack gap={6}>
                <Group justify="space-between">
                  <Text c="dimmed" size="sm">
                    Client type
                  </Text>
                  <Text fw={600} size="sm">
                    {policy.defaultClientProfile}
                  </Text>
                </Group>
                <Group justify="space-between">
                  <Text c="dimmed" size="sm">
                    Grant types
                  </Text>
                  <Text className="mw-mono" fw={600} size="sm">
                    {policy.defaultGrantTypes.join(', ')}
                  </Text>
                </Group>
                <Group justify="space-between">
                  <Text c="dimmed" size="sm">
                    PKCE
                  </Text>
                  <Text fw={600} size="sm">
                    {policy.defaultRequirePkce ? 'Required' : 'Not applicable'}
                  </Text>
                </Group>
              </Stack>
            </Box>
          )}

          {policy?.defaultGrantTypes.includes('authorization_code') && (
            <Checkbox label="Allow refresh tokens" {...form.getInputProps('allowRefreshTokens', { type: 'checkbox' })} />
          )}

          <Group justify="flex-end" gap="sm" mt="xs">
            <Button variant="default" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" loading={mutation.isPending} disabled={policiesQuery.isLoading}>
              Create application
            </Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
