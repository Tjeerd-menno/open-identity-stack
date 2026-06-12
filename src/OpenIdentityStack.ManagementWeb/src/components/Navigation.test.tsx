import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { Navigation } from './Navigation';

describe('Navigation', () => {
  it('shows retained management domains and keeps authentication settings discoverable', () => {
    renderManagementWeb(<Navigation />);

    expect(screen.getByRole('link', { name: 'Applications' })).toHaveAttribute('href', '/applications');
    expect(screen.getByRole('link', { name: 'Permissions' })).toHaveAttribute('href', '/application-permissions');
    expect(screen.getByRole('link', { name: 'Audit' })).toHaveAttribute('href', '/audit-entries');
    expect(screen.getByRole('link', { name: 'Identity providers' })).toHaveAttribute('href', '/providers');
    expect(screen.getByRole('link', { name: 'Authentication settings' })).toHaveAttribute('href', '/providers/settings');
    expect(screen.queryByRole('link', { name: /service accounts/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /clients/i })).not.toBeInTheDocument();
  });

  it('shows authentication settings without requiring provider read access', () => {
    renderManagementWeb(<Navigation permissions={['system:settings']} />);

    expect(screen.getByRole('link', { name: 'Authentication settings' })).toHaveAttribute('href', '/providers/settings');
    expect(screen.queryByRole('link', { name: 'Identity providers' })).not.toBeInTheDocument();
  });

  it('keeps overview visible for scoped operators', () => {
    renderManagementWeb(<Navigation permissions={['users:read']} />);

    expect(screen.getByRole('link', { name: 'Overview' })).toHaveAttribute('href', '/');
    expect(screen.getByRole('link', { name: 'Users' })).toHaveAttribute('href', '/users');
  });
});
