import { Button, Group, Stack, Text } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router';
import { SectionCard } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';

export function ProviderIdentityInventory({ providerId }: { providerId: string }) {
  const [page, setPage] = useState(1);
  const inventory = useQuery({
    queryKey: ['identity-migration-inventory', providerId, page],
    queryFn: () => api.users.getIdentityMigrationInventory({ providerId, page, pageSize: 20 }),
  });
  return (
    <SectionCard title="Identity migration inventory" description="Quarantined links are retained and cannot sign in. A configured password or another provider is only a candidate for independently tested access; it does not prove account control.">
      {inventory.isPending ? <Text>Loading identity inventory…</Text> : inventory.isError ? <Text c="red">{getApiErrorMessage(inventory.error)}</Text> : (
        <Stack>
          <Text size="sm">{inventory.data.totalCount} linked users</Text>
          {inventory.data.items.map((user) => (
            <Stack key={user.userId} gap={4}>
              <Link to={`/users/${user.userId}`}>{user.displayName}</Link>
              <Text size="xs">User status: {user.status}</Text>
              <Text size="sm" c={user.migrationBlocked ? 'red' : 'dimmed'}>
                {user.migrationBlocked ? 'Migration blocked: independent association evidence required' : 'No quarantined association'}
              </Text>
              {user.recoveryRequired && <Text size="sm" c="red">No independent login candidate: proof-based recovery must be delivered before migration.</Text>}
              <Text size="xs">Password credential: {user.hasPasswordCredential ? 'configured; access must be tested independently' : 'none'} · Proven federation candidates: {user.candidateFederationProviderIds.length}</Text>
              {user.identities.filter((identity) => identity.providerId === providerId).map((identity) => (
                <Text key={identity.subjectId} size="xs">{identity.isQuarantined ? 'Quarantined' : 'Association evidence recorded'} · Evidence: {identity.associationEvidence}</Text>
              ))}
            </Stack>
          ))}
          <Group>
            <Button variant="default" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous inventory page</Button>
            <Text size="sm">Page {page}</Text>
            <Button variant="default" disabled={page * 20 >= inventory.data.totalCount} onClick={() => setPage(page + 1)}>Next inventory page</Button>
          </Group>
        </Stack>
      )}
    </SectionCard>
  );
}
