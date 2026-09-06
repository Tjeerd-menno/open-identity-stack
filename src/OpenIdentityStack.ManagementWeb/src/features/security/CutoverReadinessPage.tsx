import { Alert, Button, Checkbox, Group, Loader, NumberInput, Select, Stack, Table, Text, TextInput } from '@mantine/core';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import type { ResourceTokenWindow } from '@openidentitystack/admin-api-client';
import { ErrorState, PageHeader, SectionCard } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';

export const credentialCutoverOperationIdKey = 'ois.credential-cutover.operation-id';
export const credentialCutoverSubmittedOperationIdKey = 'ois.credential-cutover.submitted-operation-id';
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

function getCredentialCutoverOperationId() {
  const stored = sessionStorage.getItem(credentialCutoverOperationIdKey);
  if (stored && uuidPattern.test(stored)) return stored;
  const operationId = crypto.randomUUID();
  sessionStorage.setItem(credentialCutoverOperationIdKey, operationId);
  return operationId;
}

function ResourceReview({ resource, refresh }: { resource: ResourceTokenWindow; refresh: () => void }) {
  const [mechanism, setMechanism] = useState<string | null>(resource.mechanism);
  const [seconds, setSeconds] = useState<number | string>(resource.residualSeconds ?? '');
  const [reference, setReference] = useState(resource.evidenceReference ?? '');
  const review = useMutation({ mutationFn: () => api.cutover.reviewResourceWindow(resource.resourceId, { mechanism: mechanism!, residualSeconds: Number(seconds), evidenceReference: reference }), onSuccess: refresh });
  return <SectionCard title={resource.displayName} description={`${resource.audience} · ${resource.scope}`}>
    <Stack>
      <Text>{resource.reviewed ? 'Current resource revision reviewed' : 'Token window review required'}</Text>
      <Text size="sm">Record tested consumer controls and the maximum residual acceptance window, including token lifetime, cache and clock skew. These external controls require independent operational verification.</Text>
      <Select label="Consumer control" value={mechanism} onChange={setMechanism} data={[{ value: 'OnlineIntrospection', label: 'Online introspection' }, { value: 'ConsumerRevocation', label: 'Consumer revocation' }, { value: 'OfflineExpiry', label: 'Offline expiry' }]} />
      <NumberInput label="Maximum residual acceptance (seconds)" value={seconds} onChange={setSeconds} min={0} max={2147483647} allowDecimal={false} />
      <TextInput label="Rehearsal evidence reference" value={reference} onChange={(event) => setReference(event.currentTarget.value)} maxLength={1000} />
      {review.isError && <ErrorState message={getApiErrorMessage(review.error)} />}
      <Button onClick={() => review.mutate()} loading={review.isPending} disabled={!mechanism || seconds === '' || !reference.trim()}>Record resource review</Button>
    </Stack>
  </SectionCard>;
}

export function CutoverReadinessPage() {
  const auth = useAuth();
  const [acknowledged, setAcknowledged] = useState(false);
  const [operationId] = useState(getCredentialCutoverOperationId);
  const [isRetry, setIsRetry] = useState(() => sessionStorage.getItem(credentialCutoverSubmittedOperationIdKey) === operationId);
  const markApprovalRetryDispatched = () => {
    sessionStorage.setItem(credentialCutoverSubmittedOperationIdKey, operationId);
    setIsRetry(true);
  };
  const execute = useMutation({
    mutationFn: () => api.cutover.execute(operationId, markApprovalRetryDispatched),
    onSuccess: () => {
      sessionStorage.removeItem(credentialCutoverOperationIdKey);
      sessionStorage.removeItem(credentialCutoverSubmittedOperationIdKey);
    },
  });
  const query = useQuery({ queryKey: ['cutover-readiness'], queryFn: api.cutover.getReadiness, enabled: !execute.isSuccess, staleTime: 0 });
  const refresh = () => { void query.refetch(); };
  const emergency = useMutation({ mutationFn: () => api.cutover.recordEmergencyAccess(), onSuccess: refresh });
  const data = query.data;
  return <Stack>
    <PageHeader title="Credential cutover readiness" description="Review current identity, administrative access and resource prerequisites before invalidating existing credentials." />
    {execute.isSuccess ? <Alert title="Credential cutover completed" color="green"><Stack>
      <Text>Operation {execute.data.operationId} completed. {execute.data.tokens} tokens, {execute.data.grants} grants and {execute.data.sessions} sessions were revoked. External consumers retain the reviewed residual windows.</Text>
      <Button onClick={() => void auth.login()}>Sign in again</Button>
    </Stack></Alert> : <>
      <Group><Button variant="default" onClick={refresh} loading={query.isFetching}>Refresh readiness</Button><Text size="sm">The server rechecks prerequisites inside the cutover transaction.</Text></Group>
      {query.isLoading && <Loader aria-label="Loading readiness" />}
      {query.isError && <ErrorState message={getApiErrorMessage(query.error)} />}
      {data && <>
        <Alert color={data.ready ? 'green' : 'orange'} title={data.ready ? 'Ready for explicit cutover approval' : 'Cutover blocked'}>
          <Stack gap="xs">{data.blockers.map((blocker) => <Text key={`${blocker.code}-${blocker.message}`}>{blocker.message}</Text>)}</Stack>
        </Alert>
        <SectionCard title="Identity inventory"><Stack>
          <Text>Quarantined links: {data.identities.quarantinedLinks}. Affected users: {data.identities.affectedUsers}. Without a configured password: {data.identities.federationOnlyUsers}. Password candidates: {data.identities.passwordCandidates}.</Text>
          <Text>A configured password is only a candidate; it does not prove independent account ownership. Quarantined links remain blocked until a separate recovery design exists.</Text>
          <Text>Disabled users: {data.identities.disabledUsers}. Verified emails: {data.identities.verifiedEmails}. Provider evidence: {data.identities.providerEvidence}. Withdrawn evidence: {data.identities.withdrawnEvidence}.</Text>
        </Stack></SectionCard>
        <SectionCard title="Independent emergency access"><Stack>
          <Text>Sign in locally with the emergency operator password, then verify within five minutes. Verification requires a live session and current unrestricted authority. Federated sign-in cannot establish this proof.</Text>
          <Text>{data.emergencyAccess?.currentlyUsable ? 'Current emergency access verified' : 'Current independent access has not been verified'}</Text>
          {emergency.isError && <ErrorState message={getApiErrorMessage(emergency.error)} />}
          <Button onClick={() => emergency.mutate()} loading={emergency.isPending}>Verify my emergency access</Button>
        </Stack></SectionCard>
        <SectionCard title="Administrative clients"><Table><Table.Thead><Table.Tr><Table.Th>Client</Table.Th><Table.Th>State</Table.Th><Table.Th>Delegated ceiling</Table.Th><Table.Th>Application permissions</Table.Th></Table.Tr></Table.Thead><Table.Tbody>
          {data.administrativeClients.map((client) => <Table.Tr key={client.id}><Table.Td>{client.clientId}</Table.Td><Table.Td>{client.active ? 'Active' : 'Disabled'} · {client.approved ? 'Approved' : 'Unapproved'}{client.requiresMigrationReview ? ' · Migration review required' : ''}</Table.Td><Table.Td>{client.delegatedPermissions.join(', ') || 'None'}</Table.Td><Table.Td>{client.applicationPermissions.join(', ') || 'None'}</Table.Td></Table.Tr>)}
        </Table.Tbody></Table></SectionCard>
        <Text>Outstanding access tokens: {data.outstandingAccessTokens}. Latest known expiry: {data.latestAccessTokenExpiry ?? 'None'}. Offline validators and relying-party sessions are not globally recalled.</Text>
        {data.businessResources.map((resource) => <ResourceReview key={`${resource.resourceId}-${resource.revision}`} resource={resource} refresh={refresh} />)}
        <SectionCard title="Execute cutover"><Stack>
          <Checkbox checked={acknowledged} onChange={(event) => setAcknowledged(event.currentTarget.checked)} label="I accept that all existing sessions and credentials will be invalidated, and accept the reviewed external residual windows." />
          {execute.isError && <ErrorState message={getApiErrorMessage(execute.error)} />}
          <Button color="red" disabled={(!isRetry && (!data.ready || !acknowledged)) || query.isError} loading={execute.isPending} onClick={() => execute.mutate()}>{isRetry ? 'Retry credential cutover' : 'Execute credential cutover'}</Button>
        </Stack></SectionCard>
      </>}
    </>}
  </Stack>;
}
