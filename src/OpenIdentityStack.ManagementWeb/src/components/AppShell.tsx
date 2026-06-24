import {
  ActionIcon,
  Anchor,
  AppShell as MantineAppShell,
  Avatar,
  Box,
  Breadcrumbs,
  Burger,
  Group,
  ScrollArea,
  Stack,
  Text,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Link, Outlet, useLocation } from 'react-router';
import { useAuth } from '@/lib/auth-context';
import { hasAnyPermission } from '@/lib/permissions';
import { BrandMark } from './BrandMark';
import { LogoutIcon } from './IamIcons';
import { ThemeToggle } from './ThemeToggle';
import { Navigation } from './Navigation';

type BreadcrumbItem = {
  label: string;
  href?: string;
};

export function AppShell() {
  const [opened, { close, toggle }] = useDisclosure();
  const auth = useAuth();
  const location = useLocation();
  const breadcrumbs = getBreadcrumbs(location.pathname).map((item) => (
    item.href === '/providers' && !hasAnyPermission(auth.permissions, ['providers:read'])
      ? { label: item.label }
      : item
  ));

  return (
    <MantineAppShell
      header={{ height: 61 }}
      navbar={{
        width: 264,
        breakpoint: 'sm',
        collapsed: { mobile: !opened },
      }}
      padding={0}
    >
      <MantineAppShell.Header>
        <Group h="100%" px="lg" gap="sm">
          <Burger
            aria-expanded={opened}
            aria-label={opened ? 'Close navigation' : 'Open navigation'}
            hiddenFrom="sm"
            onClick={toggle}
            opened={opened}
            size="sm"
          />
          <nav aria-label="Breadcrumbs" style={{ minWidth: 0 }}>
            <Breadcrumbs separatorMargin={6}>
              {breadcrumbs.map((item, index) => (
                item.href ? (
                  <Anchor component={Link} c="dimmed" key={`${item.href}-${item.label}`} size="sm" to={item.href}>
                    {item.label}
                  </Anchor>
                ) : (
                  <Text c="var(--mw-text-body)" fw={600} key={`${index}-${item.label}`} size="sm">
                    {item.label}
                  </Text>
                )
              ))}
            </Breadcrumbs>
          </nav>
        </Group>
      </MantineAppShell.Header>
      <MantineAppShell.Navbar
        style={{ background: 'var(--mw-surface-card)', display: 'flex', flexDirection: 'column' }}
      >
        <Box
          px="lg"
          style={{
            height: 60,
            display: 'flex',
            alignItems: 'center',
            borderBottom: '1px solid var(--mw-border)',
            flexShrink: 0,
          }}
        >
          <BrandMark />
        </Box>
        <ScrollArea style={{ flex: 1 }} p="md">
          <Navigation onNavigate={close} permissions={auth.permissions} />
        </ScrollArea>
        <Stack
          gap={8}
          p="md"
          style={{ borderTop: '1px solid var(--mw-border)', flexShrink: 0 }}
        >
          <ThemeToggle />
          <Group
            gap={10}
            wrap="nowrap"
            style={{
              padding: '8px 10px',
              borderRadius: 'var(--mantine-radius-md)',
              background: 'var(--mw-surface-sunken)',
            }}
          >
            <Avatar name={auth.displayName} color="initials" radius="xl" size={34} />
            <Box style={{ minWidth: 0, flex: 1 }}>
              <Text size="sm" fw={600} truncate c="var(--mw-text-strong)">
                {auth.displayName}
              </Text>
              <Text size="xs" c="dimmed" truncate>
                Operator
              </Text>
            </Box>
            <ActionIcon
              aria-label="Sign out"
              variant="subtle"
              color="gray"
              onClick={() => void auth.logout()}
            >
              <LogoutIcon />
            </ActionIcon>
          </Group>
        </Stack>
      </MantineAppShell.Navbar>
      <MantineAppShell.Main style={{ background: 'var(--mw-surface-sunken)' }}>
        <Box p={28} mih="100%">
          <Box maw={1080} mx="auto">
            <Outlet />
          </Box>
        </Box>
      </MantineAppShell.Main>
    </MantineAppShell>
  );
}

function getBreadcrumbs(pathname: string): BreadcrumbItem[] {
  const normalizedPath = normalizePath(pathname);
  const trail: BreadcrumbItem[] = [{ label: 'Overview', href: '/' }];

  if (normalizedPath === '/') {
    return [{ label: 'Overview' }];
  }

  const route = routeBreadcrumbs.find(({ pattern }) => pattern.test(normalizedPath));

  if (!route) {
    return [...trail, { label: formatSegment(normalizedPath.split('/').filter(Boolean).at(-1) ?? 'Page') }];
  }

  return [...trail, ...route.items];
}

function normalizePath(pathname: string) {
  const path = pathname.split(/[?#]/)[0] || '/';
  return path.length > 1 ? path.replace(/\/+$/, '') : path;
}

function formatSegment(segment: string) {
  return segment
    .split('-')
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

const routeBreadcrumbs: Array<{ pattern: RegExp; items: BreadcrumbItem[] }> = [
  { pattern: /^\/users$/, items: [{ label: 'Users' }] },
  { pattern: /^\/users\/create$/, items: [{ label: 'Users', href: '/users' }, { label: 'Create user' }] },
  { pattern: /^\/users\/[^/]+$/, items: [{ label: 'Users', href: '/users' }, { label: 'User details' }] },
  { pattern: /^\/users\/[^/]+\/edit$/, items: [{ label: 'Users', href: '/users' }, { label: 'Edit user' }] },
  { pattern: /^\/groups$/, items: [{ label: 'Groups' }] },
  { pattern: /^\/groups\/new$/, items: [{ label: 'Groups', href: '/groups' }, { label: 'Create group' }] },
  { pattern: /^\/groups\/[^/]+$/, items: [{ label: 'Groups', href: '/groups' }, { label: 'Group details' }] },
  { pattern: /^\/groups\/[^/]+\/edit$/, items: [{ label: 'Groups', href: '/groups' }, { label: 'Edit group' }] },
  { pattern: /^\/roles$/, items: [{ label: 'Roles' }] },
  { pattern: /^\/roles\/new$/, items: [{ label: 'Roles', href: '/roles' }, { label: 'Create role' }] },
  { pattern: /^\/roles\/[^/]+$/, items: [{ label: 'Roles', href: '/roles' }, { label: 'Role details' }] },
  { pattern: /^\/applications$/, items: [{ label: 'Applications' }] },
  { pattern: /^\/applications\/new$/, items: [{ label: 'Applications', href: '/applications' }, { label: 'Create application' }] },
  { pattern: /^\/applications\/[^/]+$/, items: [{ label: 'Applications', href: '/applications' }, { label: 'Application details' }] },
  { pattern: /^\/applications\/[^/]+\/edit$/, items: [{ label: 'Applications', href: '/applications' }, { label: 'Edit application' }] },
  { pattern: /^\/application-permissions$/, items: [{ label: 'Permissions' }] },
  { pattern: /^\/application-permissions\/new$/, items: [{ label: 'Permissions', href: '/application-permissions' }, { label: 'Add application' }] },
  { pattern: /^\/application-permissions\/[^/]+$/, items: [{ label: 'Permissions', href: '/application-permissions' }, { label: 'Application permissions' }] },
  { pattern: /^\/sessions$/, items: [{ label: 'Sessions' }] },
  { pattern: /^\/sessions\/[^/]+$/, items: [{ label: 'Sessions', href: '/sessions' }, { label: 'Session details' }] },
  { pattern: /^\/providers$/, items: [{ label: 'Identity providers' }] },
  { pattern: /^\/providers\/settings$/, items: [{ label: 'Identity providers', href: '/providers' }, { label: 'Authentication settings' }] },
  { pattern: /^\/providers\/new$/, items: [{ label: 'Identity providers', href: '/providers' }, { label: 'Create provider' }] },
  { pattern: /^\/providers\/[^/]+$/, items: [{ label: 'Identity providers', href: '/providers' }, { label: 'Provider details' }] },
  { pattern: /^\/providers\/[^/]+\/edit$/, items: [{ label: 'Identity providers', href: '/providers' }, { label: 'Edit provider' }] },
  { pattern: /^\/audit-entries$/, items: [{ label: 'Audit' }] },
];
