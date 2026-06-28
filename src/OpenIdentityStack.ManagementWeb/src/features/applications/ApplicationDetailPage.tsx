import { Badge, Group, Stack, Tabs, Text } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useParams } from 'react-router';
import { BackLink, CenteredState, DetailHeader, ErrorState, FieldRow, MetaStrip, SectionCard, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { formatDateTime } from '@/lib/format';

export function ApplicationDetailPage() {
  const { applicationId = '' } = useParams();

  const appQuery = useQuery({
    queryKey: ['application', applicationId],
    queryFn: () => api.applications.getApplication(applicationId),
  });
  const credentialsQuery = useQuery({
    queryKey: ['application', applicationId, 'credentials'],
    queryFn: () => api.applications.listApplicationCredentials(applicationId),
  });

  if (appQuery.isLoading) {
    return <CenteredState loading title="Loading application…" />;
  }
  if (appQuery.isError || !appQuery.data) {
    return <ErrorState message={getApiErrorMessage(appQuery.error)} />;
  }

  const app = appQuery.data;
  const credentials = credentialsQuery.data ?? [];

  return (
    <div>
      <BackLink label="Back to applications" to="/applications" />
      <DetailHeader
        icon={app.allowedGrantTypes.includes('client_credentials') ? 'server' : 'app-window'}
        title={app.displayName}
        description={app.clientId}
        badge={<StatusBadge status={app.status} />}
      />

      <MetaStrip
        items={[
          { label: 'Profile', value: app.profile },
          { label: 'Client type', value: app.clientType },
          { label: 'Scopes', value: app.allowedScopes.length },
          { label: 'Created', value: formatDateTime(app.createdAt) },
        ]}
      />

      <Tabs defaultValue="config" color="blue" keepMounted={false}>
        <Tabs.List mb="lg">
          <Tabs.Tab value="config">Configuration</Tabs.Tab>
          <Tabs.Tab value="oauth">Scopes &amp; grants</Tabs.Tab>
          <Tabs.Tab value="uris">Redirect URIs</Tabs.Tab>
          <Tabs.Tab value="credentials">Credentials ({credentials.length})</Tabs.Tab>
        </Tabs.List>

        <Tabs.Panel value="config">
          <SectionCard title="Configuration">
            <FieldRow label="Client ID" value={app.clientId} mono />
            <FieldRow label="Profile" value={app.profile} />
            <FieldRow label="Client type" value={app.clientType} />
            <FieldRow label="PKCE" value={app.requirePkce ? 'Required' : 'Not required'} />
            <FieldRow label="Consent" value={app.requireConsent ? 'Required' : 'Not required'} />
            <FieldRow label="Created" value={formatDateTime(app.createdAt)} last />
          </SectionCard>
        </Tabs.Panel>

        <Tabs.Panel value="oauth">
          <Stack gap="lg">
            <SectionCard title="Grant types" description="OAuth flows this client is permitted to use.">
              <Group gap="xs">
                {app.allowedGrantTypes.length === 0 ? (
                  <Text c="dimmed" size="sm">None configured.</Text>
                ) : (
                  app.allowedGrantTypes.map((grant) => (
                    <Badge key={grant} color="gray" variant="outline" style={{ fontFamily: 'var(--mw-mono)' }}>
                      {grant}
                    </Badge>
                  ))
                )}
              </Group>
            </SectionCard>
            <SectionCard title="Allowed scopes" description="Scopes this client may request at the token endpoint.">
              <Group gap="xs">
                {app.allowedScopes.length === 0 ? (
                  <Text c="dimmed" size="sm">No scopes.</Text>
                ) : (
                  app.allowedScopes.map((scope) => (
                    <Badge key={scope} color="blue" variant="light" style={{ fontFamily: 'var(--mw-mono)' }}>
                      {scope}
                    </Badge>
                  ))
                )}
              </Group>
            </SectionCard>
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="uris">
          <Stack gap="lg">
            <SectionCard title="Redirect URIs">
              {app.redirectUris.length === 0 ? (
                <Text c="dimmed" size="sm">None — this client does not redirect a user.</Text>
              ) : (
                <Stack gap={0}>
                  {app.redirectUris.map((uri, index) => (
                    <FieldRow key={uri} label={`URI ${index + 1}`} value={uri} mono last={index === app.redirectUris.length - 1} />
                  ))}
                </Stack>
              )}
            </SectionCard>
            <SectionCard title="Post-logout redirect URIs">
              {app.postLogoutRedirectUris.length === 0 ? (
                <Text c="dimmed" size="sm">None configured.</Text>
              ) : (
                <Stack gap={0}>
                  {app.postLogoutRedirectUris.map((uri, index) => (
                    <FieldRow key={uri} label={`URI ${index + 1}`} value={uri} mono last={index === app.postLogoutRedirectUris.length - 1} />
                  ))}
                </Stack>
              )}
            </SectionCard>
          </Stack>
        </Tabs.Panel>

        <Tabs.Panel value="credentials">
          <SectionCard title="Credentials" description="Client secrets and certificates registered to this application.">
            {credentials.length === 0 ? (
              <Text c="dimmed" size="sm">No credentials.</Text>
            ) : (
              <Stack gap={0}>
                {credentials.map((credential, index) => (
                  <FieldRow
                    key={credential.id}
                    label={credential.type === 'ClientSecret' ? 'Client secret' : 'Certificate'}
                    value={
                      credential.revokedAt
                        ? 'Revoked'
                        : credential.expiresAt
                          ? `Expires ${formatDateTime(credential.expiresAt)}`
                          : 'Active'
                    }
                    last={index === credentials.length - 1}
                  />
                ))}
              </Stack>
            )}
          </SectionCard>
        </Tabs.Panel>
      </Tabs>
    </div>
  );
}
