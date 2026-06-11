import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { Navigation } from './Navigation';

describe('Navigation', () => {
  it('shows retained management domains and omits legacy application surfaces', () => {
    renderManagementWeb(<Navigation />);

    expect(screen.getByRole('link', { name: 'Applications' })).toHaveAttribute('href', '/applications');
    expect(screen.getByRole('link', { name: 'Permissions' })).toHaveAttribute('href', '/application-permissions');
    expect(screen.getByRole('link', { name: 'Audit' })).toHaveAttribute('href', '/audit-entries');
    expect(screen.getByRole('link', { name: 'Identity providers' })).toHaveAttribute('href', '/providers');
    expect(screen.queryByRole('link', { name: /settings/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /service accounts/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /clients/i })).not.toBeInTheDocument();
  });
});
