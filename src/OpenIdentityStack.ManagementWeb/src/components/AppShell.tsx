import {
  ActionIcon,
  AppShell as MantineAppShell,
  Avatar,
  Box,
  Burger,
  Group,
  Menu,
  NavLink,
  Stack,
  Text,
  ThemeIcon,
  Tooltip,
  UnstyledButton,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Link, Outlet, useLocation } from 'react-router';
import { useAuth } from '@/lib/auth-context';
import { hasAnyPermission } from '@/lib/permissions';
import { getInitials } from '@/lib/format';
import { useThemePreference } from './ThemeProvider';
import { Icon, type IconName } from './Icon';

type NavItem = {
  label: string;
  to: string;
  icon: IconName;
  match: string[];
  permissions?: string[];
};

const NAV: NavItem[] = [
  { label: 'Overview', to: '/', icon: 'layout-dashboard', match: ['/'] },
  { label: 'Users', to: '/users', icon: 'users', match: ['/users'], permissions: ['users:read'] },
  { label: 'Groups', to: '/groups', icon: 'users-round', match: ['/groups'], permissions: ['groups:read'] },
  { label: 'Roles', to: '/roles', icon: 'shield', match: ['/roles'], permissions: ['roles:read'] },
  {
    label: 'Permissions',
    to: '/application-permissions',
    icon: 'list-checks',
    match: ['/application-permissions'],
    permissions: ['application-permissions:read'],
  },
  { label: 'Applications', to: '/applications', icon: 'app-window', match: ['/applications'], permissions: ['applications:read'] },
  { label: 'Sessions', to: '/sessions', icon: 'activity', match: ['/sessions'], permissions: ['sessions:read'] },
  { label: 'Identity providers', to: '/providers', icon: 'globe', match: ['/providers'], permissions: ['providers:read'] },
  {
    label: 'Authentication settings',
    to: '/providers/settings',
    icon: 'settings',
    match: ['/providers/settings'],
    permissions: ['system:settings'],
  },
  { label: 'Audit', to: '/audit-entries', icon: 'scroll-text', match: ['/audit-entries'], permissions: ['audit-logs:read'] },
];

function isActive(pathname: string, item: NavItem): boolean {
  if (pathname.startsWith('/providers/settings') && item.to === '/providers') {
    return false;
  }
  if (item.to === '/') {
    return pathname === '/';
  }
  return item.match.some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}

export function AppShell() {
  const [opened, { close, toggle }] = useDisclosure();
  const auth = useAuth();
  const { resolvedTheme, toggle: toggleTheme } = useThemePreference();
  const location = useLocation();

  const visibleNav = NAV.filter((item) => !item.permissions || hasAnyPermission(auth.permissions, item.permissions));
  const current = [...visibleNav].reverse().find((item) => isActive(location.pathname, item));

  return (
    <MantineAppShell
      layout="alt"
      header={{ height: 61 }}
      navbar={{ width: 264, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding={0}
    >
      <MantineAppShell.Header>
        <Group h="100%" px="lg" gap="sm" wrap="nowrap">
          <Burger
            aria-expanded={opened}
            aria-label={opened ? 'Close navigation' : 'Open navigation'}
            hiddenFrom="sm"
            onClick={toggle}
            opened={opened}
            size="sm"
          />
          <nav aria-label="Breadcrumbs">
            <Group gap={8} wrap="nowrap">
              <Text c="dimmed" size="sm">
                Management
              </Text>
              <Icon name="chevron-right" size={14} color="var(--mw-text-muted)" />
              <Text fw={600} size="sm">
                {current?.label ?? 'Overview'}
              </Text>
            </Group>
          </nav>
        </Group>
      </MantineAppShell.Header>

      <MantineAppShell.Navbar>
        <MantineAppShell.Section>
          <Group h={61} px="lg" gap="xs" wrap="nowrap" style={{ borderBottom: '1px solid var(--mw-border)' }}>
            <ThemeIcon color="blue" radius="md" size={30} variant="filled">
              <Icon name="shield-check" size={18} stroke={2.2} />
            </ThemeIcon>
            <Text fw={700} size="md" style={{ letterSpacing: '-0.2px' }}>
              OpenIdentity
              <Text c="blue" component="span" fw={700} inherit>
                Stack
              </Text>
            </Text>
          </Group>
        </MantineAppShell.Section>

        <MantineAppShell.Section grow p="sm" style={{ overflowY: 'auto' }}>
          <nav aria-label="Management navigation">
            <Stack gap={2}>
              {visibleNav.map((item) => (
                <NavLink
                  key={item.to}
                  active={isActive(location.pathname, item)}
                  component={Link}
                  label={item.label}
                  leftSection={<Icon name={item.icon} size={18} />}
                  onClick={close}
                  to={item.to}
                />
              ))}
            </Stack>
          </nav>
        </MantineAppShell.Section>

        <MantineAppShell.Section p="sm" style={{ borderTop: '1px solid var(--mw-border)' }}>
          <Group gap="xs" justify="space-between" mb="xs">
            <Text c="dimmed" size="xs">
              Appearance
            </Text>
            <Tooltip label={resolvedTheme === 'dark' ? 'Light appearance' : 'Dark appearance'} withArrow>
              <ActionIcon aria-label="Toggle appearance" onClick={toggleTheme} variant="subtle" color="gray">
                <Icon name={resolvedTheme === 'dark' ? 'sun' : 'moon'} size={18} />
              </ActionIcon>
            </Tooltip>
          </Group>
          <Menu position="top-end" shadow="md" width={200}>
            <Menu.Target>
              <UnstyledButton
                aria-label="Operator menu"
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 10,
                  width: '100%',
                  padding: '8px 10px',
                  borderRadius: 'var(--mantine-radius-md)',
                  background: 'var(--mw-surface-sunken)',
                }}
              >
                <Avatar color="blue" name={auth.displayName} size={34}>
                  {getInitials(auth.displayName)}
                </Avatar>
                <Box style={{ minWidth: 0, flex: 1 }}>
                  <Text fw={600} size="sm" truncate>
                    {auth.displayName}
                  </Text>
                  <Text c="dimmed" size="xs" truncate>
                    Operator
                  </Text>
                </Box>
                <Icon name="log-out" size={16} color="var(--mw-text-muted)" />
              </UnstyledButton>
            </Menu.Target>
            <Menu.Dropdown>
              <Menu.Label>{auth.displayName}</Menu.Label>
              <Menu.Item leftSection={<Icon name="log-out" size={16} />} onClick={() => void auth.logout()}>
                Sign out
              </Menu.Item>
            </Menu.Dropdown>
          </Menu>
        </MantineAppShell.Section>
      </MantineAppShell.Navbar>

      <MantineAppShell.Main style={{ background: 'var(--mw-surface-sunken)' }}>
        <Box mx="auto" p={28} style={{ maxWidth: 1080 }}>
          <Outlet />
        </Box>
      </MantineAppShell.Main>
    </MantineAppShell>
  );
}
