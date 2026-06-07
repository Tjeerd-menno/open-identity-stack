import { Badge, Button, Card, Group, Progress, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { Link } from 'react-router';
import { PageHeader } from '@/components/PagePrimitives';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';

type OverviewPageProps = {
  permissions?: string[];
};

type OverviewSection = {
  label: string;
  path: string;
  permission: string;
  description: string;
};

const overviewSections: OverviewSection[] = [
  {
    label: 'Users',
    path: '/users',
    permission: 'users:read',
    description: 'Accounts, status, roles, groups, and upstream identities.',
  },
  {
    label: 'Roles',
    path: '/roles',
    permission: 'roles:read',
    description: 'Role catalog, platform permissions, and assignment rules.',
  },
  {
    label: 'Groups',
    path: '/groups',
    permission: 'groups:read',
    description: 'Group membership and external mapping management.',
  },
  {
    label: 'Applications',
    path: '/applications',
    permission: 'applications:read',
    description: 'Consolidated OAuth and OIDC applications.',
  },
  {
    label: 'Permissions',
    path: '/application-permissions',
    permission: 'application-permissions:read',
    description: 'Application permission registry, manifests, and diagnostics.',
  },
  {
    label: 'Sessions',
    path: '/sessions',
    permission: 'sessions:read',
    description: 'Active, expired, revoked, and logged-out user sessions.',
  },
  {
    label: 'Identity providers',
    path: '/providers',
    permission: 'providers:read',
    description: 'OIDC provider configuration and lifecycle.',
  },
  {
    label: 'Settings',
    path: '/settings',
    permission: 'system:settings',
    description: 'Authentication defaults and local fallback controls.',
  },
  {
    label: 'Audit',
    path: '/audit-entries',
    permission: 'audit-logs:read',
    description: 'Read-only administrative audit trail.',
  },
];

export function OverviewPage({ permissions }: OverviewPageProps) {
  if (permissions) {
    return <OverviewContent permissions={permissions} />;
  }

  return <OverviewFromAuth />;
}

function OverviewFromAuth() {
  const auth = useAuth();
  return <OverviewContent permissions={auth.permissions} />;
}

function OverviewContent({ permissions }: { permissions: string[] }) {
  const effectivePermissions = permissions;
  const availableSections = overviewSections.filter((section) => hasPermission(effectivePermissions, section.permission));
  const unavailableCount = overviewSections.length - availableSections.length;

  return (
    <Stack gap="lg">
      <PageHeader
        title="Overview"
        description="Operational IAM console for identity, access, application, federation, and audit workflows."
        badges={[
          { label: `${availableSections.length} available`, color: 'green' },
          { label: `${unavailableCount} unavailable`, color: unavailableCount === 0 ? 'gray' : 'yellow' },
        ]}
      />

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }}>
        <MetricCard label="Accessible surfaces" value={`${availableSections.length}/${overviewSections.length}`} tone="green" />
        <MetricCard label="Access coverage" value={`${Math.round((availableSections.length / overviewSections.length) * 100)}%`} tone="blue" />
        <MetricCard label="Security signals" value={hasPermission(effectivePermissions, 'audit-logs:read') ? 'Audit ready' : 'Limited'} tone="teal" />
        <MetricCard label="Operator scope" value={hasPermission(effectivePermissions, '*') ? 'Full' : 'Scoped'} tone="gray" />
      </SimpleGrid>

      <nav aria-label="Overview quick links">
        <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
          {overviewSections.map((section) => (
            <OverviewSectionCard
              key={section.path}
              section={section}
              isAvailable={hasPermission(effectivePermissions, section.permission)}
            />
          ))}
        </SimpleGrid>
      </nav>

      <SimpleGrid cols={{ base: 1, lg: 3 }}>
        <Card withBorder radius="sm" padding="md">
          <Stack gap="xs">
            <Title order={2} size="h4">Access Summary</Title>
            <Progress value={(availableSections.length / overviewSections.length) * 100} color="blue" />
            <Text size="sm" c="dimmed">
              Route visibility is based on effective permissions in the current operator token.
            </Text>
          </Stack>
        </Card>
        <Card withBorder radius="sm" padding="md">
          <Stack gap="xs">
            <Title order={2} size="h4">Recent Signals</Title>
            <Text size="sm">Audit trail: {hasPermission(effectivePermissions, 'audit-logs:read') ? 'Available' : 'No access'}</Text>
            <Text size="sm">Session controls: {hasPermission(effectivePermissions, 'sessions:read') ? 'Available' : 'No access'}</Text>
            <Text size="sm">Provider lifecycle: {hasPermission(effectivePermissions, 'providers:read') ? 'Available' : 'No access'}</Text>
          </Stack>
        </Card>
        <Card withBorder radius="sm" padding="md">
          <Stack gap="xs">
            <Title order={2} size="h4">Quick Actions</Title>
            {availableSections.slice(0, 3).map((section) => (
              <Button key={section.path} component={Link} to={section.path} variant="subtle">
                {section.label}
              </Button>
            ))}
          </Stack>
        </Card>
      </SimpleGrid>
    </Stack>
  );
}

function MetricCard({ label, value, tone }: { label: string; value: string; tone: string }) {
  return (
    <Card withBorder radius="sm" padding="md" style={{ borderTop: `3px solid var(--mantine-color-${tone}-5)` }}>
      <Stack gap={4}>
        <Text size="xs" c="dimmed" fw={650} tt="uppercase">{label}</Text>
        <Title order={2} size="h3">{value}</Title>
      </Stack>
    </Card>
  );
}

function OverviewSectionCard({
  section,
  isAvailable,
}: {
  section: OverviewSection;
  isAvailable: boolean;
}) {
  return (
    <Card aria-label={section.label} component="article" withBorder radius="sm" padding="md">
      <Stack gap="sm">
        <Group justify="space-between" align="flex-start">
          <Title order={2} size="h4">{section.label}</Title>
          <Badge color={isAvailable ? 'green' : 'gray'} variant="light">
            {isAvailable ? 'Available' : 'No access'}
          </Badge>
        </Group>
        <Text size="sm" c="dimmed">{section.description}</Text>
        {isAvailable ? (
          <Button component={Link} to={section.path} variant="light" size="sm">
            Open {section.label}
          </Button>
        ) : (
          <Text size="sm">Requires {section.permission}</Text>
        )}
      </Stack>
    </Card>
  );
}
