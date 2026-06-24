import { Anchor, Badge, Group, Paper, SimpleGrid, Stack, Text, ThemeIcon, type MantineColor } from '@mantine/core';
import type { ComponentType, SVGProps } from 'react';
import { Link } from 'react-router';
import { PageHeader, StatCard } from '@/components/PagePrimitives';
import {
  ApplicationsIcon,
  ArrowRightIcon,
  AuditIcon,
  GroupsIcon,
  PermissionsIcon,
  ProvidersIcon,
  RolesIcon,
  SessionsIcon,
  SettingsIcon,
  ShieldIcon,
  UsersIcon,
} from '@/components/IamIcons';
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
  icon: ComponentType<SVGProps<SVGSVGElement>>;
  color: MantineColor;
};

const overviewSections: OverviewSection[] = [
  {
    label: 'Users',
    path: '/users',
    permission: 'users:read',
    description: 'Accounts, status, roles, groups, and upstream identities.',
    icon: UsersIcon,
    color: 'blue',
  },
  {
    label: 'Roles',
    path: '/roles',
    permission: 'roles:read',
    description: 'Role catalog, platform permissions, and assignment rules.',
    icon: RolesIcon,
    color: 'violet',
  },
  {
    label: 'Groups',
    path: '/groups',
    permission: 'groups:read',
    description: 'Group membership and external mapping management.',
    icon: GroupsIcon,
    color: 'teal',
  },
  {
    label: 'Applications',
    path: '/applications',
    permission: 'applications:read',
    description: 'Consolidated OAuth and OIDC applications.',
    icon: ApplicationsIcon,
    color: 'cyan',
  },
  {
    label: 'Permissions',
    path: '/application-permissions',
    permission: 'application-permissions:read',
    description: 'Application permission registry, manifests, and diagnostics.',
    icon: PermissionsIcon,
    color: 'grape',
  },
  {
    label: 'Sessions',
    path: '/sessions',
    permission: 'sessions:read',
    description: 'Active, expired, revoked, and logged-out user sessions.',
    icon: SessionsIcon,
    color: 'green',
  },
  {
    label: 'Identity providers',
    path: '/providers',
    permission: 'providers:read',
    description: 'OIDC provider configuration and lifecycle management.',
    icon: ProvidersIcon,
    color: 'orange',
  },
  {
    label: 'Authentication settings',
    path: '/providers/settings',
    permission: 'system:settings',
    description: 'Authentication defaults and sign-in policy controls.',
    icon: SettingsIcon,
    color: 'indigo',
  },
  {
    label: 'Audit',
    path: '/audit-entries',
    permission: 'audit-logs:read',
    description: 'Read-only administrative audit trail.',
    icon: AuditIcon,
    color: 'gray',
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
  const coverage = Math.round((availableSections.length / overviewSections.length) * 100);

  return (
    <Stack gap="xl">
      <PageHeader
        title="Overview"
        description="Operational IAM console for identity, access, application, federation, and audit workflows."
        badges={[
          { label: `${availableSections.length} available`, color: 'green' },
          { label: `${unavailableCount} unavailable`, color: unavailableCount === 0 ? 'gray' : 'yellow' },
        ]}
      />

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }}>
        <StatCard
          label="Accessible surfaces"
          value={`${availableSections.length}/${overviewSections.length}`}
          hint="Areas your token can open"
          icon={<ShieldIcon />}
          color="blue"
        />
        <StatCard
          label="Access coverage"
          value={`${coverage}%`}
          hint="Share of the console available"
          icon={<PermissionsIcon />}
          color="teal"
        />
        <StatCard
          label="Security signals"
          value={hasPermission(effectivePermissions, 'audit-logs:read') ? 'Audit ready' : 'Limited'}
          hint="Audit trail visibility"
          icon={<AuditIcon />}
          color="green"
        />
        <StatCard
          label="Operator scope"
          value={hasPermission(effectivePermissions, '*') ? 'Full' : 'Scoped'}
          hint="Effective permission breadth"
          icon={<RolesIcon />}
          color="violet"
        />
      </SimpleGrid>

      <Stack gap="sm">
        <div>
          <Text fw={700} style={{ color: 'var(--mw-text-strong)' }}>Domains</Text>
          <Text size="sm" c="dimmed">Jump to the areas your permissions grant.</Text>
        </div>
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
  const Icon = section.icon;
  return (
    <Paper
      component="article"
      aria-label={section.label}
      withBorder
      radius="sm"
      p="lg"
      h="100%"
      className="mw-section-tile"
    >
      <Stack gap="sm" h="100%">
        <Group justify="space-between" align="flex-start" wrap="nowrap">
          <ThemeIcon color={section.color} variant="light" size={38} radius="md">
            <Icon />
          </ThemeIcon>
          <Badge color={isAvailable ? 'green' : 'gray'} variant="light">
            {isAvailable ? 'Available' : 'No access'}
          </Badge>
        </Group>
        <div>
          <Text fw={650} style={{ color: 'var(--mw-text-strong)' }}>{section.label}</Text>
          <Text size="sm" c="dimmed">{section.description}</Text>
        </div>
        {isAvailable ? (
          <Anchor component={Link} to={section.path} mt="auto" fw={500} size="sm" underline="never">
            <Group gap={6}>
              Open {section.label}
              <ArrowRightIcon />
            </Group>
          </Anchor>
        ) : (
          <Text size="sm" c="dimmed" mt="auto">Requires {section.permission}</Text>
        )}
      </Stack>
    </Paper>
  );
}
