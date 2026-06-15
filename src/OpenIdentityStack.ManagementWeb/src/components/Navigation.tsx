import { ActionIcon, Avatar, Button, Group, NavLink, Stack, Text, ThemeIcon } from '@mantine/core';
import type { ComponentType } from 'react';
import { Link, useLocation } from 'react-router';
import { hasAnyPermission } from '@/lib/permissions';
import { useThemePreference } from './ThemeProvider';
import {
  ApplicationsIcon,
  AuditIcon,
  GroupsIcon,
  LogoutIcon,
  MoonIcon,
  OverviewIcon,
  PermissionsIcon,
  ProvidersIcon,
  RolesIcon,
  SessionsIcon,
  SettingsIcon,
  SunIcon,
  UsersIcon,
} from './IamIcons';

type NavigationItem = {
  label: string;
  to: string;
  icon: ComponentType;
  match?: string[];
  requiredPermissions?: string[];
};

type NavigationProps = {
  onNavigate?: () => void;
  permissions?: string[];
  displayName?: string;
  onLogout?: () => void;
};

const navigationGroups: Array<{ label: string; items: NavigationItem[] }> = [
  {
    label: 'Identity',
    items: [
      { label: 'Overview', to: '/', icon: OverviewIcon, match: ['/'] },
      { label: 'Users', to: '/users', icon: UsersIcon, match: ['/users'], requiredPermissions: ['users:read'] },
      { label: 'Groups', to: '/groups', icon: GroupsIcon, match: ['/groups'], requiredPermissions: ['groups:read'] },
    ],
  },
  {
    label: 'Access Control',
    items: [
      { label: 'Roles', to: '/roles', icon: RolesIcon, match: ['/roles'], requiredPermissions: ['roles:read'] },
      { label: 'Permissions', to: '/application-permissions', icon: PermissionsIcon, match: ['/application-permissions'], requiredPermissions: ['application-permissions:read'] },
    ],
  },
  {
    label: 'Applications',
    items: [
      { label: 'Applications', to: '/applications', icon: ApplicationsIcon, match: ['/applications'], requiredPermissions: ['applications:read'] },
      { label: 'Sessions', to: '/sessions', icon: SessionsIcon, match: ['/sessions'], requiredPermissions: ['sessions:read'] },
    ],
  },
  {
    label: 'Federation',
    items: [
      { label: 'Identity providers', to: '/providers', icon: ProvidersIcon, match: ['/providers'], requiredPermissions: ['providers:read'] },
      { label: 'Authentication settings', to: '/providers/settings', icon: SettingsIcon, match: ['/providers/settings'], requiredPermissions: ['system:settings'] },
    ],
  },
  {
    label: 'Operations',
    items: [
      { label: 'Audit', to: '/audit-entries', icon: AuditIcon, match: ['/audit-entries'], requiredPermissions: ['audit-logs:read'] },
    ],
  },
];

export function Navigation({ onNavigate, permissions = ['*'], displayName = '', onLogout }: NavigationProps) {
  const location = useLocation();
  const { resolvedTheme, preference, setPreference } = useThemePreference();
  const isDark = resolvedTheme === 'dark';

  function toggleTheme() {
    if (preference === 'system') {
      setPreference(isDark ? 'light' : 'dark');
    } else {
      setPreference(isDark ? 'light' : 'dark');
    }
  }

  return (
    <Stack h="100%" gap={0} style={{ overflow: 'hidden' }}>
      {/* Brand header — matches sidebar header height with the topbar */}
      <Group
        gap={10}
        px="md"
        h={61}
        style={{ borderBottom: '1px solid var(--mantine-color-default-border)', flexShrink: 0 }}
      >
        <ThemeIcon radius="md" size={32} color="blue" variant="filled">
          <RolesIcon />
        </ThemeIcon>
        <Text fw={700} size="sm" lh={1}>
          OpenIdentity
          <Text component="span" c="blue" fw={700} size="sm" inherit>Stack</Text>
        </Text>
      </Group>

      {/* Nav items */}
      <nav aria-label="Management navigation" style={{ flex: 1, overflowY: 'auto', padding: '12px 12px' }}>
        <Stack gap="md">
          {navigationGroups.map((group) => {
            const visibleItems = group.items.filter(
              (item) => !item.requiredPermissions || hasAnyPermission(permissions, item.requiredPermissions),
            );

            if (visibleItems.length === 0) {
              return null;
            }

            return (
              <Stack key={group.label} gap={4}>
                <Text c="dimmed" fw={600} size="xs" tt="uppercase" px="xs">
                  {group.label}
                </Text>
                {visibleItems.map((item) => {
                  const Icon = item.icon;
                  return (
                    <NavLink
                      key={item.to}
                      active={isActivePath(location.pathname, item)}
                      component={Link}
                      label={item.label}
                      leftSection={<Icon />}
                      onClick={onNavigate}
                      to={item.to}
                    />
                  );
                })}
              </Stack>
            );
          })}
        </Stack>
      </nav>

      {/* Footer: theme toggle + user card */}
      <Stack
        gap="xs"
        p="sm"
        style={{ borderTop: '1px solid var(--mantine-color-default-border)', flexShrink: 0 }}
      >
        <Button
          variant="subtle"
          color="gray"
          fullWidth
          justify="flex-start"
          leftSection={isDark ? <SunIcon /> : <MoonIcon />}
          onClick={toggleTheme}
          aria-label={isDark ? 'Switch to light appearance' : 'Switch to dark appearance'}
        >
          {isDark ? 'Light appearance' : 'Dark appearance'}
        </Button>
        <Group
          gap="sm"
          px="sm"
          py="xs"
          style={{
            borderRadius: 'var(--mantine-radius-md)',
            background: 'var(--mantine-color-default-hover)',
          }}
        >
          <Avatar name={displayName || 'Operator'} color="blue" size="sm" />
          <Stack gap={0} style={{ flex: 1, minWidth: 0 }}>
            <Text size="sm" fw={600} truncate>{displayName || 'Operator'}</Text>
            <Text size="xs" c="dimmed" truncate>Operator</Text>
          </Stack>
          {onLogout && (
            <ActionIcon variant="subtle" color="gray" aria-label="Sign out" onClick={onLogout}>
              <LogoutIcon />
            </ActionIcon>
          )}
        </Group>
      </Stack>
    </Stack>
  );
}

function isActivePath(pathname: string, item: NavigationItem) {
  if (pathname.startsWith('/providers/settings') && item.to === '/providers') {
    return false;
  }

  if (item.to === '/') {
    return pathname === '/';
  }

  return (item.match ?? [item.to]).some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}
