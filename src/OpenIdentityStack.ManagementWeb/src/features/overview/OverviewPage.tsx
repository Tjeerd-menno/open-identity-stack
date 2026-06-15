import { Card, Group, SimpleGrid, Stack, Text, ThemeIcon, Title, UnstyledButton } from '@mantine/core';
import { Link } from 'react-router';
import { PageHeader } from '@/components/PagePrimitives';
import {
  ApplicationsIcon,
  ArrowRightIcon,
  AuditIcon,
  GroupsIcon,
  OverviewIcon,
  PermissionsIcon,
  ProvidersIcon,
  RolesIcon,
  SessionsIcon,
  SettingsIcon,
  UsersIcon,
} from '@/components/IamIcons';
import { useAuth } from '@/lib/auth-context';
import { hasPermission } from '@/lib/permissions';
import type { ComponentType } from 'react';

type OverviewSection = {
  label: string;
  path: string;
  permission: string;
  description: string;
  icon: ComponentType;
  color: string;
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
    color: 'green',
  },
  {
    label: 'Groups',
    path: '/groups',
    permission: 'groups:read',
    description: 'Group membership and external mapping management.',
    icon: GroupsIcon,
    color: 'grape',
  },
  {
    label: 'Applications',
    path: '/applications',
    permission: 'applications:read',
    description: 'Consolidated OAuth and OIDC applications.',
    icon: ApplicationsIcon,
    color: 'teal',
  },
  {
    label: 'Permissions',
    path: '/application-permissions',
    permission: 'application-permissions:read',
    description: 'Application permission registry, manifests, and diagnostics.',
    icon: PermissionsIcon,
    color: 'violet',
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
    color: 'gray',
  },
  {
    label: 'Audit',
    path: '/audit-entries',
    permission: 'audit-logs:read',
    description: 'Read-only administrative audit trail.',
    icon: AuditIcon,
    color: 'yellow',
  },
];

type OverviewPageProps = {
  permissions?: string[];
};

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
  const available = overviewSections.filter((s) => hasPermission(permissions, s.permission));
  const unavailable = overviewSections.filter((s) => !hasPermission(permissions, s.permission));

  return (
    <Stack gap="lg">
      <PageHeader
        title="Overview"
        description="A summary of your identity tenant and quick access to the domains you administer."
      />

      <SimpleGrid cols={{ base: 1, sm: 2 }} spacing="md">
        {/* Domains quick-access card */}
        <Card withBorder radius="md" padding="lg">
          <Stack gap="xs" mb="md">
            <Group gap="xs">
              <ThemeIcon color="blue" size={28} radius="md" variant="light">
                <OverviewIcon />
              </ThemeIcon>
              <Title order={2} size="h4">Domains</Title>
            </Group>
            <Text size="sm" c="dimmed">Jump to the areas your permissions grant.</Text>
          </Stack>

          <SimpleGrid cols={2} spacing="xs">
            {available.map((section) => (
              <DomainButton key={section.path} section={section} />
            ))}
            {unavailable.map((section) => (
              <DomainButton key={section.path} section={section} disabled />
            ))}
          </SimpleGrid>
        </Card>

        {/* Access summary card */}
        <Card withBorder radius="md" padding="lg">
          <Stack gap="xs" mb="md">
            <Group gap="xs">
              <ThemeIcon color="teal" size={28} radius="md" variant="light">
                <RolesIcon />
              </ThemeIcon>
              <Title order={2} size="h4">Access summary</Title>
            </Group>
            <Text size="sm" c="dimmed">Effective operator permissions in this session.</Text>
          </Stack>

          <Stack gap="xs">
            {overviewSections.map((section) => {
              const isAccessible = hasPermission(permissions, section.permission);
              const Icon = section.icon;
              return (
                <Group key={section.path} gap="sm" justify="space-between">
                  <Group gap="xs">
                    <ThemeIcon
                      color={isAccessible ? section.color : 'gray'}
                      size={24}
                      radius="sm"
                      variant="light"
                    >
                      <Icon />
                    </ThemeIcon>
                    <Text size="sm" c={isAccessible ? 'inherit' : 'dimmed'}>{section.label}</Text>
                  </Group>
                  <Text size="xs" c={isAccessible ? 'green' : 'dimmed'} fw={600}>
                    {isAccessible ? 'Accessible' : 'No access'}
                  </Text>
                </Group>
              );
            })}
          </Stack>
        </Card>
      </SimpleGrid>
    </Stack>
  );
}

function DomainButton({ section, disabled }: { section: OverviewSection; disabled?: boolean }) {
  const Icon = section.icon;

  if (disabled) {
    return (
      <Group
        gap="xs"
        p="sm"
        style={{
          border: '1px solid var(--mantine-color-default-border)',
          borderRadius: 'var(--mantine-radius-md)',
          opacity: 0.45,
          cursor: 'not-allowed',
        }}
      >
        <ThemeIcon color="gray" size={32} radius="md" variant="light">
          <Icon />
        </ThemeIcon>
        <Text size="sm" fw={600} style={{ flex: 1 }}>{section.label}</Text>
      </Group>
    );
  }

  return (
    <UnstyledButton
      component={Link}
      to={section.path}
      p="sm"
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        border: '1px solid var(--mantine-color-default-border)',
        borderRadius: 'var(--mantine-radius-md)',
        background: 'var(--mantine-color-body)',
        textDecoration: 'none',
        transition: 'border-color 120ms, background 120ms',
      }}
      onMouseEnter={(e) => {
        const el = e.currentTarget as HTMLElement;
        el.style.borderColor = 'var(--mantine-color-blue-5)';
        el.style.background = 'var(--mantine-color-blue-0)';
      }}
      onMouseLeave={(e) => {
        const el = e.currentTarget as HTMLElement;
        el.style.borderColor = 'var(--mantine-color-default-border)';
        el.style.background = 'var(--mantine-color-body)';
      }}
    >
      <ThemeIcon color={section.color} size={32} radius="md" variant="light">
        <Icon />
      </ThemeIcon>
      <Text size="sm" fw={600} style={{ flex: 1, color: 'inherit' }}>{section.label}</Text>
      <Text c="dimmed" style={{ display: 'flex', flexShrink: 0 }}>
        <ArrowRightIcon width="1em" height="1em" />
      </Text>
    </UnstyledButton>
  );
}
