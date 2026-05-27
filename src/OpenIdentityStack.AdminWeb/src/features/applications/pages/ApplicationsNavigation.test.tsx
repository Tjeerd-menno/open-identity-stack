import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { Sidebar } from '@/components/layout/Sidebar';
import { router } from '@/routes';

vi.mock('@/features/auth/services/oidc-config', () => ({
  oidcConfig: {},
  extractPermissions: vi.fn(() => []),
  extractDisplayName: vi.fn(() => 'Mock User'),
}));

describe('Applications navigation', () => {
  it('shows Applications as the only application management navigation label', () => {
    render(<Sidebar />, { wrapper: MemoryRouter });

    expect(screen.getByRole('link', { name: /applications/i })).toHaveAttribute('href', '/applications');
    expect(screen.queryByRole('link', { name: /clients/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /service accounts/i })).not.toBeInTheDocument();
  });

  it.each([
    '/clients',
    '/clients/new',
    '/clients/:id',
    '/clients/:id/edit',
    '/service-accounts',
    '/service-accounts/new',
    '/service-accounts/:id',
    '/service-accounts/:id/edit',
  ])('does not configure legacy application route %s', (legacyPath) => {
    const rootRoute = router.routes.find((route) => route.path === '/');
    const route = rootRoute?.children?.find((child) => child.path === legacyPath.slice(1));

    expect(route).toBeUndefined();
  });
});
