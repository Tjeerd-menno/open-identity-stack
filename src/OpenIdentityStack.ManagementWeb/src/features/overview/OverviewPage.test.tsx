import { screen, within } from '@testing-library/react';
import { renderManagementWeb } from '@/test/render';
import { OverviewPage } from './OverviewPage';

describe('OverviewPage', () => {
  it('shows available management sections and quick links without legacy surfaces', () => {
    renderManagementWeb(
      <OverviewPage
        permissions={[
          'users:read',
          'roles:read',
          'groups:read',
          'applications:read',
          'application-permissions:read',
          'sessions:read',
          'providers:read',
          'system:settings',
          'audit-logs:read',
        ]}
      />
    );

    expect(screen.getByRole('heading', { name: /^overview$/i })).toBeInTheDocument();
    expect(screen.getByText('8 available')).toBeInTheDocument();
    expect(screen.queryByText(/current token/i)).not.toBeInTheDocument();

    const quickLinks = screen.getByRole('navigation', { name: /overview quick links/i });
    expect(within(quickLinks).getByRole('link', { name: /users/i })).toHaveAttribute('href', '/users');
    expect(within(quickLinks).getByRole('link', { name: /applications/i })).toHaveAttribute('href', '/applications');
    expect(within(quickLinks).getByRole('link', { name: /audit/i })).toHaveAttribute('href', '/audit-entries');
    expect(within(quickLinks).queryByRole('link', { name: /settings/i })).not.toBeInTheDocument();

    const usersCard = screen.getByRole('article', { name: /users/i });
    expect(within(usersCard).getByRole('link', { name: /open users/i })).toHaveAttribute('href', '/users');

    expect(screen.queryByRole('link', { name: /clients/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /service accounts/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('article', { name: /settings/i })).not.toBeInTheDocument();
  });

  it('marks sections unavailable when the operator lacks permission', () => {
    renderManagementWeb(<OverviewPage permissions={['users:read']} />);

    expect(screen.getByText('1 available')).toBeInTheDocument();
    expect(screen.getByText('7 unavailable')).toBeInTheDocument();
    expect(screen.getByText('Requires applications:read')).toBeInTheDocument();
    const quickLinks = screen.getByRole('navigation', { name: /overview quick links/i });
    expect(within(quickLinks).getByRole('link', { name: /users/i })).toHaveAttribute('href', '/users');
    expect(within(quickLinks).queryByRole('link', { name: /applications/i })).not.toBeInTheDocument();
  });
});
