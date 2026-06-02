import { Badge, Button, Card, Group, SimpleGrid, Stack, Text, Title } from '@mantine/core';
import { Link } from 'react-router';
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
      <div>
        <Title order={1}>Overview</Title>
        <Text c="dimmed">ManagementWeb parity dashboard for retained operator domains.</Text>
      </div>

      <Group gap="sm">
        <Badge color="green" variant="light" size="lg">{availableSections.length} available</Badge>
        <Badge color={unavailableCount === 0 ? 'gray' : 'yellow'} variant="light" size="lg">
          {unavailableCount} unavailable
        </Badge>
      </Group>

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
    </Stack>
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
    <Card withBorder radius="sm" padding="md">
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
            {section.label}
          </Button>
        ) : (
          <Text size="sm">Requires {section.permission}</Text>
        )}
      </Stack>
    </Card>
  );
}
