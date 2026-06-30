import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { AppShell } from './AppShell';

describe('AppShell', () => {
  it('renders the full navigation for an operator with all permissions', () => {
    renderManagementWeb(<AppShell />, { auth: makeAuth(), initialEntries: ['/'] });

    const nav = screen.getByRole('navigation', { name: /management navigation/i });
    expect(within(nav).getByRole('link', { name: 'Overview' })).toHaveAttribute('href', '/');
    expect(within(nav).getByRole('link', { name: 'Users' })).toHaveAttribute('href', '/users');
    expect(within(nav).getByRole('link', { name: 'Applications' })).toHaveAttribute('href', '/applications');
    expect(within(nav).getByRole('link', { name: 'Audit' })).toHaveAttribute('href', '/audit-entries');
    expect(screen.getByRole('button', { name: /operator menu/i })).toBeInTheDocument();
  });

  it('hides navigation items the operator lacks permission for', () => {
    renderManagementWeb(<AppShell />, { auth: makeAuth({ permissions: ['users:read'] }), initialEntries: ['/users'] });

    const nav = screen.getByRole('navigation', { name: /management navigation/i });
    expect(within(nav).getByRole('link', { name: 'Overview' })).toBeInTheDocument();
    expect(within(nav).getByRole('link', { name: 'Users' })).toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: 'Roles' })).not.toBeInTheDocument();
    expect(within(nav).queryByRole('link', { name: 'Audit' })).not.toBeInTheDocument();
  });

  it('shows the management breadcrumb for the current section', () => {
    renderManagementWeb(<AppShell />, { auth: makeAuth(), initialEntries: ['/applications'] });

    const breadcrumbs = screen.getByRole('navigation', { name: /breadcrumbs/i });
    expect(within(breadcrumbs).getByText('Management')).toBeInTheDocument();
    expect(within(breadcrumbs).getByText('Applications')).toBeInTheDocument();
  });
});
