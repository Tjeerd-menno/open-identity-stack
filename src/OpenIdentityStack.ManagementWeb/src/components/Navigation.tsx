import { Stack, Text, NavLink } from '@mantine/core';
import type { ComponentType } from 'react';
import { Link, useLocation } from 'react-router';
import { hasAnyPermission } from '@/lib/permissions';
import {
  ApplicationsIcon,
  AuditIcon,
  GroupsIcon,
  OverviewIcon,
  PermissionsIcon,
  ProvidersIcon,
  RolesIcon,
  SessionsIcon,
  SettingsIcon,
  UsersIcon,
} from './IamIcons';

type NavigationItem = {
  label: string;
  to: string;
  icon: ComponentType;
  match?: string[];
  requiredPermissions: string[];
};

type NavigationProps = {
  onNavigate?: () => void;
  permissions?: string[];
};

const navigationGroups: Array<{ label: string; items: NavigationItem[] }> = [
  {
    label: 'Identity',
    items: [
      { label: 'Overview', to: '/', icon: OverviewIcon, match: ['/'], requiredPermissions: ['*'] },
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

export function Navigation({ onNavigate, permissions = ['*'] }: NavigationProps) {
  const location = useLocation();

  return (
    <nav aria-label="Management navigation">
      <Stack gap="md">
        {navigationGroups.map((group) => {
          const visibleItems = group.items.filter((item) => hasAnyPermission(permissions, item.requiredPermissions));

          if (visibleItems.length === 0) {
            return null;
          }

          return (
          <Stack key={group.label} gap={4}>
            <Text c="dimmed" fw={650} size="xs" tt="uppercase">
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
