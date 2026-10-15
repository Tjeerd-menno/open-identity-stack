import { Card, Group, SimpleGrid, Stack, Text, ThemeIcon, UnstyledButton } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { Icon, type IconName } from '@/components/Icon';
import { PageHeader, SectionCard, StatCard } from '@/components/primitives';
import { api } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import {
  applicationsRead,
  auditLogsRead,
  groupsRead,
  providersRead,
  rolesRead,
  sessionsRead,
  usersRead,
} from '@/lib/generated-permissions';
import { hasPermission } from '@/lib/permissions';
import { formatRelativeTime } from '@/lib/format';

type DomainLink = { label: string; to: string; icon: IconName; permission: string; description: string };

const DOMAINS: DomainLink[] = [
  { label: 'Users', to: '/users', icon: 'users', permission: usersRead, description: 'Accounts, status, roles and upstream identities.' },
  { label: 'Roles', to: '/roles', icon: 'shield', permission: rolesRead, description: 'Role catalog and platform permission grants.' },
  { label: 'Groups', to: '/groups', icon: 'users-round', permission: groupsRead, description: 'Membership and external mappings.' },
  { label: 'Applications', to: '/applications', icon: 'app-window', permission: applicationsRead, description: 'OAuth/OIDC client registrations.' },
  { label: 'Sessions', to: '/sessions', icon: 'activity', permission: sessionsRead, description: 'Active, expired and revoked sessions.' },
  { label: 'Identity providers', to: '/providers', icon: 'globe', permission: providersRead, description: 'Upstream provider configuration.' },
];

export function OverviewPage() {
  const auth = useAuth();
  const can = (permission: string) => hasPermission(auth.permissions, permission);

  const usersQuery = useQuery({
    queryKey: ['overview', 'users'],
    queryFn: () => api.users.getUsers({ page: 1, pageSize: 1 }),
    enabled: can(usersRead),
  });
  const appsQuery = useQuery({
    queryKey: ['overview', 'applications'],
    queryFn: () => api.applications.getApplications({ page: 1, pageSize: 1 }),
    enabled: can(applicationsRead),
  });
  const sessionsQuery = useQuery({
    queryKey: ['overview', 'sessions'],
    queryFn: () => api.sessions.getSessions({ page: 1, pageSize: 1, status: 'Active' }),
    enabled: can(sessionsRead),
  });
  const providersQuery = useQuery({
    queryKey: ['overview', 'providers'],
    queryFn: () => api.providers.getProviders(true),
    enabled: can(providersRead),
  });
  const auditQuery = useQuery({
    queryKey: ['overview', 'audit'],
    queryFn: () => api.audit.getAuditEntries({ page: 1, pageSize: 6 }),
    enabled: can(auditLogsRead),
  });

  const stat = (value: number | undefined, can: boolean) => (can ? (value ?? '—') : '—');
  const visibleDomains = DOMAINS.filter((domain) => can(domain.permission));

  return (
    <div>
      <PageHeader
        title="Overview"
        description="A summary of your identity tenant and quick access to the domains you administer."
      />

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} mb="lg">
        <StatCard label="Users" value={stat(usersQuery.data?.totalCount, can(usersRead))} icon="users" color="blue" />
        <StatCard label="Applications" value={stat(appsQuery.data?.totalCount, can(applicationsRead))} icon="app-window" color="teal" />
        <StatCard
          label="Active sessions"
          value={stat(sessionsQuery.data?.totalCount, can(sessionsRead))}
          icon="activity"
          color="violet"
        />
        <StatCard
          label="Identity providers"
          value={stat(providersQuery.data?.length, can(providersRead))}
          icon="globe"
          color="green"
        />
      </SimpleGrid>

      <SimpleGrid cols={{ base: 1, lg: 2 }} spacing="lg" style={{ alignItems: 'start' }}>
        <SectionCard title="Domains" description="Jump to the areas your permissions grant.">
          <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="sm">
            {visibleDomains.map((domain) => (
              <UnstyledButton
                key={domain.to}
                component={Link}
                to={domain.to}
                style={{
                  display: 'flex',
                  gap: 12,
                  padding: 12,
                  borderRadius: 'var(--mantine-radius-md)',
                  border: '1px solid var(--mw-border)',
                }}
              >
                <ThemeIcon color="blue" radius="md" size={36} variant="light">
                  <Icon name={domain.icon} size={18} />
                </ThemeIcon>
                <div style={{ minWidth: 0 }}>
                  <Text fw={600} size="sm">
                    {domain.label}
                  </Text>
                  <Text c="dimmed" size="xs" lineClamp={2}>
                    {domain.description}
                  </Text>
                </div>
              </UnstyledButton>
            ))}
          </SimpleGrid>
        </SectionCard>

        <SectionCard title="Recent activity" description="The latest entries from the administrative audit trail.">
          {can(auditLogsRead) ? (
              <Stack gap={0}>
                {(auditQuery.data?.items ?? []).map((entry, index, array) => (
                  <Group
                    key={entry.id}
                    align="flex-start"
                    gap="sm"
                    py="sm"
                    wrap="nowrap"
                    style={{ borderBottom: index === array.length - 1 ? undefined : '1px solid var(--mw-border)' }}
                  >
                    <ThemeIcon color="gray" radius="md" size={32} variant="light">
                      <Icon name="scroll-text" size={16} />
                    </ThemeIcon>
                    <div style={{ minWidth: 0, flex: 1 }}>
                      <Text className="mw-mono" size="sm" truncate>
                        {entry.action}
                      </Text>
                      <Text c="dimmed" size="xs">
                        {entry.entityType} · {formatRelativeTime(entry.timestamp)}
                      </Text>
                    </div>
                  </Group>
                ))}
                {(auditQuery.data?.items.length ?? 0) === 0 && (
                  <Text c="dimmed" py="sm" size="sm">
                    No recent activity.
                  </Text>
                )}
              </Stack>
            ) : (
              <Text c="dimmed" size="sm">
                Requires audit-logs:read.
              </Text>
            )}
          </SectionCard>
      </SimpleGrid>

      {can(auditLogsRead) === false && providersQuery.data === undefined && (
        <Card mt="lg" padding="lg">
          <Text c="dimmed" size="sm">
            Your operator scope is limited. Areas you can access appear in the sidebar.
          </Text>
        </Card>
      )}
    </div>
  );
}
