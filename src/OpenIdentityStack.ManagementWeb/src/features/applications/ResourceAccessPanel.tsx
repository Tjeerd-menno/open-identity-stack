import { Alert, Button, Checkbox, Group, Modal, Select, Stack, Text, TextInput, Textarea } from '@mantine/core';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import type { ProtectedResource, ResourceConfiguration } from '@openidentitystack/admin-api-client';
import { SectionCard } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';

const lines = (value: string) => [...new Set(value.split(/[\n,]/).map((item) => item.trim()).filter(Boolean))];

export function ResourceAccessPanel({ applicationId, canWrite }: { applicationId: string; canWrite: boolean }) {
  const queryClient = useQueryClient();
  const resources = useQuery({ queryKey: ['protected-resources'], queryFn: () => api.applications.listProtectedResources() });
  const grants = useQuery({ queryKey: ['application', applicationId, 'resource-grants'], queryFn: () => api.applications.listClientResourceGrants(applicationId) });
  const [selected, setSelected] = useState<string | null>(null);
  const [editing, setEditing] = useState<ProtectedResource | 'new' | null>(null);
  const [delegated, setDelegated] = useState('');
  const [machine, setMachine] = useState('');
  const [expectedRevision, setExpectedRevision] = useState<number | undefined>();
  const resource = resources.data?.find((item) => item.id === selected);
  const save = useMutation({
    mutationFn: () => api.applications.configureClientResourceGrant(applicationId, selected!, {
      delegatedPermissions: lines(delegated), applicationPermissions: lines(machine), expectedRevision,
    }),
    onSuccess: (saved) => { setExpectedRevision(saved.revision); void queryClient.invalidateQueries({ queryKey: ['application', applicationId, 'resource-grants'] }); },
  });
  const choose = (id: string | null) => {
    setSelected(id);
    const existing = grants.data?.find((item) => item.resourceId === id);
    setDelegated(existing?.delegatedPermissions.join('\n') ?? '');
    setMachine(existing?.applicationPermissions.join('\n') ?? '');
    setExpectedRevision(existing?.revision);
    save.reset();
  };
  const failed = resources.error ?? grants.error ?? save.error;
  return <SectionCard title="Resource access">
    <Stack>
      <Text size="sm">A resource defines a token audience and permission namespaces. Delegated ceilings limit user permissions; application permissions authorize machine tokens. OAuth scopes alone grant no permissions.</Text>
      {failed && <Alert color="red">{getApiErrorMessage(failed)}</Alert>}
      {canWrite && <Button variant="light" onClick={() => setEditing('new')}>Add protected resource</Button>}
      <Select label="Protected resource" placeholder="Choose a resource" value={selected} onChange={choose}
        disabled={resources.isPending || grants.isPending || save.isPending} data={(resources.data ?? []).map((item) => ({ value: item.id, label: item.displayName }))} />
      {resource && <>
        <Text size="sm">Audience: {resource.audience}</Text>
        <Text size="sm">Request scope: {resource.scope}. Namespaces: {resource.permissionNamespaces.join(', ')}</Text>
        {!resource.enabled && <Alert color="yellow">This resource is disabled. Token requests will be rejected.</Alert>}
        {resource.isAdministrative ? <Alert>Administrative access requires its dedicated approval workflow.</Alert> : <>
          {canWrite && <Button variant="subtle" onClick={() => setEditing(resource)}>Edit resource mapping</Button>}
          <Textarea label="Delegated permission ceiling" description="One permission or terminal wildcard per line. An empty ceiling grants no delegated permissions." value={delegated} onChange={(event) => setDelegated(event.currentTarget.value)} readOnly={!canWrite} minRows={3} />
          <Textarea label="Application permissions" description="Explicit permissions for client credentials. These do not inherit user roles." value={machine} onChange={(event) => setMachine(event.currentTarget.value)} readOnly={!canWrite} minRows={3} />
          {canWrite && <Button onClick={() => save.mutate()} loading={save.isPending}>Save resource grant</Button>}
          {save.isSuccess && <Text role="status">Resource grant saved.</Text>}
        </>}
      </>}
      {editing && <ResourceEditor key={editing === 'new' ? 'new' : editing.id} resource={editing === 'new' ? null : editing} close={() => setEditing(null)} />}
    </Stack>
  </SectionCard>;
}

function ResourceEditor({ resource, close }: { resource: ProtectedResource | null; close: () => void }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState(resource?.displayName ?? '');
  const [audience, setAudience] = useState(resource?.audience ?? '');
  const [scope, setScope] = useState(resource?.scope ?? '');
  const [namespaces, setNamespaces] = useState(resource?.permissionNamespaces.join('\n') ?? '');
  const [enabled, setEnabled] = useState(resource?.enabled ?? true);
  const save = useMutation({
    mutationFn: () => {
      const data: ResourceConfiguration = { displayName: name, audience, scope, permissionNamespaces: lines(namespaces), enabled, expectedRevision: resource?.revision };
      return resource ? api.applications.configureProtectedResource(resource.id, data) : api.applications.createProtectedResource(data);
    },
    onSuccess: () => { void queryClient.invalidateQueries({ queryKey: ['protected-resources'] }); close(); },
  });
  return <Modal opened onClose={close} title={resource ? 'Edit protected resource' : 'Add protected resource'}>
    <form onSubmit={(event) => { event.preventDefault(); save.mutate(); }}><Stack>
      {save.error && <Alert color="red">{getApiErrorMessage(save.error)}</Alert>}
      <TextInput label="Display name" required value={name} onChange={(event) => setName(event.currentTarget.value)} />
      <TextInput label="Audience URI" description="An HTTPS URL or URN. Immutable after creation." required readOnly={!!resource} value={audience} onChange={(event) => setAudience(event.currentTarget.value)} />
      <TextInput label="Resource scope" required readOnly={!!resource} value={scope} onChange={(event) => setScope(event.currentTarget.value)} />
      <Textarea label="Permission namespaces" description="Existing registered application identifiers, one per line." required value={namespaces} onChange={(event) => setNamespaces(event.currentTarget.value)} />
      <Checkbox label="Enabled" checked={enabled} onChange={(event) => setEnabled(event.currentTarget.checked)} />
      <Group justify="flex-end"><Button variant="default" onClick={close}>Cancel</Button><Button type="submit" loading={save.isPending}>Save resource</Button></Group>
    </Stack></form>
  </Modal>;
}
