import { Alert, Badge, Button, Stack, Text, Textarea } from '@mantine/core';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, getApiErrorMessage } from '@/lib/api';
import { CenteredState, ErrorState, SectionCard } from '@/components/primitives';
import type { AdministrativeAccess } from '@openidentitystack/admin-api-client';

export function AdministrativeAccessPanel({ applicationId, canWrite }: { applicationId: string; canWrite: boolean }) {
  const query = useQuery({ queryKey: ['application', applicationId, 'administrative-access'], queryFn: () => api.administrativeAccess.get(applicationId) });
  if (query.isPending) { return <CenteredState loading title="Loading administrative access…" />; }
  if (query.isError) { return <ErrorState message={getApiErrorMessage(query.error)} />; }
  return <AdministrativeAccessForm key={query.data.revision ?? 'unapproved'} applicationId={applicationId} canWrite={canWrite} access={query.data} />;
}

function AdministrativeAccessForm({ applicationId, canWrite, access }: { applicationId: string; canWrite: boolean; access: AdministrativeAccess }) {
  const [delegated, setDelegated] = useState(access.delegatedPermissions.join('\n'));
  const [machine, setMachine] = useState(access.applicationPermissions.join('\n'));
  const queryClient = useQueryClient();
  const save = useMutation({
    mutationFn: () => api.administrativeAccess.save(applicationId, {
      delegatedPermissions: parsePermissions(delegated), applicationPermissions: parsePermissions(machine), expectedRevision: access.revision,
    }),
    onSuccess: (result) => queryClient.setQueryData(['application', applicationId, 'administrative-access'], result),
  });
  return (
    <SectionCard title="Administrative access" description="Administrative tokens target only the Admin API. Request a separate token for business resources.">
      <Stack>
        <Badge color={access.approved ? 'green' : 'gray'}>{access.approved ? 'Approved' : 'Not approved'}</Badge>
        <Text size="sm">Initial approval and permission increases require a freshly authenticated administrator who holds the explicit all-permissions grant. Machine integrations cannot approve access.</Text>
        <Text size="sm">Enter one platform permission per line. * includes every current and future permission. Clear both ceilings to withdraw access.</Text>
        <Textarea label="Delegated permission ceiling" description="Human access also requires the user's own permissions." value={delegated} onChange={event => setDelegated(event.currentTarget.value)} disabled={!canWrite} autosize minRows={3} />
        <Textarea label="Machine permission ceiling" description="Explicit permissions for client credentials; no human privileges are inherited." value={machine} onChange={event => setMachine(event.currentTarget.value)} disabled={!canWrite} autosize minRows={3} />
        {save.isError && <Alert color="red" role="alert">{getApiErrorMessage(save.error)}</Alert>}
        {canWrite && <Button onClick={() => save.mutate()} loading={save.isPending}>Save administrative access</Button>}
      </Stack>
    </SectionCard>
  );
}

function parsePermissions(value: string) { return [...new Set(value.split(/[\n,]/).map(permission => permission.trim()).filter(Boolean))]; }
