import { screen } from '@testing-library/react';
import { renderManagementWeb } from '@/test/render';
import { OverviewPage } from './OverviewPage';

describe('OverviewPage', () => {
  it('shows available domains as links without legacy surfaces', () => {
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
    expect(screen.queryByText(/current token/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /clients/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /service accounts/i })).not.toBeInTheDocument();

    // All sections should be accessible links in the domains grid
    expect(screen.getByRole('link', { name: /users/i })).toHaveAttribute('href', '/users');
    expect(screen.getByRole('link', { name: /applications/i })).toHaveAttribute('href', '/applications');
    expect(screen.getByRole('link', { name: /audit/i })).toHaveAttribute('href', '/audit-entries');
    expect(screen.getByRole('link', { name: /authentication settings/i })).toHaveAttribute('href', '/providers/settings');
  });

  it('marks sections unavailable when the operator lacks permission', () => {
    renderManagementWeb(<OverviewPage permissions={['users:read']} />);

    // Users domain should be a link (accessible)
    expect(screen.getByRole('link', { name: /users/i })).toHaveAttribute('href', '/users');

    // Applications domain should NOT be a link (no access)
    expect(screen.queryByRole('link', { name: /^applications$/i })).not.toBeInTheDocument();

    // Access summary shows accessible/no-access text
    expect(screen.getAllByText('Accessible').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('No access').length).toBeGreaterThanOrEqual(1);
  });

  it('shows authentication settings for settings-only operators', () => {
    renderManagementWeb(<OverviewPage permissions={['system:settings']} />);

    // Authentication settings should be accessible
    expect(screen.getByRole('link', { name: /authentication settings/i })).toHaveAttribute('href', '/providers/settings');

    // Identity providers should NOT be accessible
    expect(screen.queryByRole('link', { name: /^identity providers$/i })).not.toBeInTheDocument();
  });
});
