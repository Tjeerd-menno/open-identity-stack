import { ActionIcon, Anchor, AppShell as MantineAppShell, Badge, Breadcrumbs, Burger, Group, Menu, Stack, Text, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Link, Outlet, useLocation } from 'react-router';
import { useAuth } from '@/lib/auth-context';
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
  const breadcrumbs = getBreadcrumbs(location.pathname);

  return (
    <MantineAppShell
      header={{ height: 64 }}
      navbar={{
        width: 280,
        breakpoint: 'sm',
        collapsed: { mobile: !opened },
      }}
      padding="lg"
    >
      <MantineAppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Group gap="sm">
            <Burger
              aria-expanded={opened}
              aria-label={opened ? 'Close navigation' : 'Open navigation'}
              hiddenFrom="sm"
              onClick={toggle}
              opened={opened}
              size="sm"
            />
            <div>
              <Title order={3} size="h4">OpenIdentityStack</Title>
              <Text size="xs" c="dimmed">
                IAM Console
              </Text>
            </div>
            <Badge visibleFrom="sm" color="teal" variant="light">Local tenant</Badge>
          </Group>
          <Group gap="xs" justify="flex-end">
            <ThemeToggle />
            <Menu position="bottom-end" shadow="md">
              <Menu.Target>
                <ActionIcon aria-label="Operator menu" variant="light">
                  {auth.displayName.slice(0, 1).toUpperCase()}
                </ActionIcon>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>{auth.displayName}</Menu.Label>
                <Menu.Item leftSection={<LogoutIcon />} onClick={() => void auth.logout()}>
                  Sign out
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </Group>
        </Group>
      </MantineAppShell.Header>
      <MantineAppShell.Navbar p="md">
        <Navigation onNavigate={close} />
      </MantineAppShell.Navbar>
      <MantineAppShell.Main>
        <Stack gap="lg">
          <nav aria-label="Breadcrumbs">
            <Breadcrumbs>
              {breadcrumbs.map((item, index) => (
                item.href ? (
                  <Anchor component={Link} key={`${item.href}-${item.label}`} size="sm" to={item.href}>
                    {item.label}
                  </Anchor>
                ) : (
                  <Text c="dimmed" fw={600} key={`${index}-${item.label}`} size="sm">
                    {item.label}
                  </Text>
                )
              ))}
            </Breadcrumbs>
          </nav>
          <Outlet />
        </Stack>
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
