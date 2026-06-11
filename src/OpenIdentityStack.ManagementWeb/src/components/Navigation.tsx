import { Stack, Text, NavLink } from '@mantine/core';
import type { ComponentType } from 'react';
import { Link, useLocation } from 'react-router';
import {
  ApplicationsIcon,
  AuditIcon,
  GroupsIcon,
  OverviewIcon,
  PermissionsIcon,
  ProvidersIcon,
  RolesIcon,
  SessionsIcon,
  UsersIcon,
} from './IamIcons';

type NavigationItem = {
  label: string;
  to: string;
  icon: ComponentType;
  match?: string[];
};

type NavigationProps = {
  onNavigate?: () => void;
};

const navigationGroups: Array<{ label: string; items: NavigationItem[] }> = [
  {
    label: 'Identity',
    items: [
      { label: 'Overview', to: '/', icon: OverviewIcon, match: ['/'] },
      { label: 'Users', to: '/users', icon: UsersIcon, match: ['/users'] },
      { label: 'Groups', to: '/groups', icon: GroupsIcon, match: ['/groups'] },
    ],
  },
  {
    label: 'Access Control',
    items: [
      { label: 'Roles', to: '/roles', icon: RolesIcon, match: ['/roles'] },
      { label: 'Permissions', to: '/application-permissions', icon: PermissionsIcon, match: ['/application-permissions'] },
    ],
  },
  {
    label: 'Applications',
    items: [
      { label: 'Applications', to: '/applications', icon: ApplicationsIcon, match: ['/applications'] },
      { label: 'Sessions', to: '/sessions', icon: SessionsIcon, match: ['/sessions'] },
    ],
  },
  {
    label: 'Federation',
    items: [
      { label: 'Identity providers', to: '/providers', icon: ProvidersIcon, match: ['/providers'] },
    ],
  },
  {
    label: 'Operations',
    items: [
      { label: 'Audit', to: '/audit-entries', icon: AuditIcon, match: ['/audit-entries'] },
    ],
  },
];

export function Navigation({ onNavigate }: NavigationProps) {
  const location = useLocation();

  return (
    <nav aria-label="Management navigation">
      <Stack gap="md">
        {navigationGroups.map((group) => (
          <Stack key={group.label} gap={4}>
            <Text c="dimmed" fw={650} size="xs" tt="uppercase">
              {group.label}
            </Text>
            {group.items.map((item) => {
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
        ))}
      </Stack>
    </nav>
  );
}

function isActivePath(pathname: string, item: NavigationItem) {
  if (item.to === '/') {
    return pathname === '/';
  }

  return (item.match ?? [item.to]).some((prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`));
}
