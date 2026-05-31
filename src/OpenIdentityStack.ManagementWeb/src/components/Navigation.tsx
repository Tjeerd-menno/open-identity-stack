import { NavLink } from '@mantine/core';
import { Link, useLocation } from 'react-router';

const navigationItems = [
  { label: 'Overview', to: '/' },
  { label: 'Users', to: '/users' },
  { label: 'Roles', to: '/roles' },
  { label: 'Groups', to: '/groups' },
  { label: 'Service accounts', to: '/service-accounts' },
  { label: 'Identity providers', to: '/identity-providers' },
  { label: 'Audit', to: '/audit' },
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
