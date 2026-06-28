import { Alert, Button, Group, Modal, Stack, TagsInput, Text } from '@mantine/core';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import type { Application } from '@openidentitystack/admin-api-client';
import { Icon } from '@/components/Icon';
import { UriList } from '@/components/UriList';
import { api, getApiErrorMessage } from '@/lib/api';

export function EditOAuthModal({ app, onSaved, onClose }: { app: Application; onSaved: () => void; onClose: () => void }) {
  const queryClient = useQueryClient();
  const usesRedirects = app.allowedGrantTypes.includes('authorization_code');
  const [redirectUris, setRedirectUris] = useState<string[]>(app.redirectUris.length ? app.redirectUris : ['']);
  const [postLogoutUris, setPostLogoutUris] = useState<string[]>(
    app.postLogoutRedirectUris.length ? app.postLogoutRedirectUris : ['']
  );
  const [scopes, setScopes] = useState<string[]>(app.allowedScopes);
  const [redirectError, setRedirectError] = useState<string | undefined>(undefined);

  const mutation = useMutation({
    mutationFn: () =>
      api.applications.configureApplicationOAuth(app.id, {
        profile: app.profile,
        clientType: app.clientType,
        allowedGrantTypes: app.allowedGrantTypes,
        allowedScopes: scopes,
        redirectUris: redirectUris.map((uri) => uri.trim()).filter(Boolean),
        postLogoutRedirectUris: postLogoutUris.map((uri) => uri.trim()).filter(Boolean),
        requirePkce: app.requirePkce,
        requireConsent: app.requireConsent,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['application', app.id] });
      onSaved();
      onClose();
    },
  });

  const submit = () => {
    if (usesRedirects && redirectUris.every((uri) => !uri.trim())) {
      setRedirectError('At least one redirect URI is required for this profile.');
      return;
    }
    setRedirectError(undefined);
    mutation.mutate();
  };

  return (
    <Modal opened onClose={onClose} title="Edit OAuth configuration" centered size="lg">
      <Stack gap="md">
        {mutation.isError && (
          <Alert color="red" icon={<Icon name="circle-alert" size={18} />} variant="light">
            {getApiErrorMessage(mutation.error)}
          </Alert>
        )}

        {usesRedirects ? (
          <>
            <UriList
              label="Redirect URIs *"
              description="Where the authorization server returns the user after sign-in."
              values={redirectUris}
              onChange={setRedirectUris}
              placeholder="https://app.example.com/callback"
              error={redirectError}
            />
            <UriList
              label="Post-logout redirect URIs"
              description="Optional. Where users may be sent after signing out."
              values={postLogoutUris}
              onChange={setPostLogoutUris}
              placeholder="https://app.example.com/signed-out"
            />
          </>
        ) : (
          <Alert color="blue" icon={<Icon name="server" size={18} />} variant="light">
            This client uses <code style={{ fontFamily: 'var(--mw-mono)' }}>client_credentials</code> and never redirects a user, so it has no redirect URIs.
          </Alert>
        )}

        <TagsInput
          label="Allowed scopes"
          description="Scopes this client may request at the token endpoint. Press Enter to add."
          placeholder="Add a scope"
          value={scopes}
          onChange={setScopes}
          styles={{ input: { fontFamily: 'var(--mw-mono)' } }}
        />

        <Text c="dimmed" size="xs">
          Profile, client type, grant types and PKCE stay fixed by the application profile ({app.profile}).
        </Text>

        <Group justify="flex-end" gap="sm" mt="xs">
          <Button variant="default" onClick={onClose}>
            Cancel
          </Button>
          <Button loading={mutation.isPending} onClick={submit}>
            Save configuration
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
