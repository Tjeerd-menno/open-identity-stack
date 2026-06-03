import { NavLink } from '@mantine/core';
import { Link, useLocation } from 'react-router';

const navigationItems = [
  { label: 'Overview', to: '/' },
  { label: 'Users', to: '/users' },
  { label: 'Roles', to: '/roles' },
  { label: 'Groups', to: '/groups' },
  { label: 'Applications', to: '/applications' },
  { label: 'Permissions', to: '/application-permissions' },
  { label: 'Sessions', to: '/sessions' },
  { label: 'Identity providers', to: '/providers' },
  { label: 'Settings', to: '/settings' },
  { label: 'Audit', to: '/audit-entries' },
];

export function Navigation() {
  const location = useLocation();

  return (
    <nav aria-label="Management navigation">
      {navigationItems.map((item) => (
        <NavLink
          key={item.to}
          component={Link}
          to={item.to}
          label={item.label}
          active={location.pathname === item.to}
        />
      ))}
    </nav>
  );
}
