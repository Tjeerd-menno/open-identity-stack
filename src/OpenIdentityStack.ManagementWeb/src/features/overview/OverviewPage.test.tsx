import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, page, resetApiMocks } from '@/test/mock-api';
import { OverviewPage } from './OverviewPage';

vi.mock('@/lib/api', async () => {
  const { mockApi } = await import('@/test/mock-api');
  return {
    api: mockApi,
    getApiErrorMessage: (error: unknown) => (error instanceof Error ? error.message : String(error)),
    isApiError: () => false,
  };
});

beforeEach(() => {
  resetApiMocks();
  mockApi.users.getUsers.mockResolvedValue(page([], { totalCount: 3 }));
  mockApi.applications.getApplications.mockResolvedValue(page([], { totalCount: 7 }));
  mockApi.sessions.getSessions.mockResolvedValue(page([], { totalCount: 6 }));
  mockApi.providers.getProviders.mockResolvedValue([{ id: 'p1' }, { id: 'p2' }]);
  mockApi.audit.getAuditEntries.mockResolvedValue(page([]));
});

describe('OverviewPage', () => {
  it('shows tenant stat counts from the list endpoints', async () => {
    renderManagementWeb(<OverviewPage />, { auth: makeAuth() });

    expect(await screen.findByText('3')).toBeInTheDocument(); // Users
    expect(screen.getByText('7')).toBeInTheDocument(); // Applications
    expect(screen.getByText('6')).toBeInTheDocument(); // Active sessions
  });

  it('links to the domains the operator can access', async () => {
    renderManagementWeb(<OverviewPage />, { auth: makeAuth() });

    expect(await screen.findByRole('link', { name: /users/i })).toHaveAttribute('href', '/users');
    expect(screen.getByRole('link', { name: /applications/i })).toHaveAttribute('href', '/applications');
  });

  it('hides domains the operator lacks permission for', async () => {
    renderManagementWeb(<OverviewPage />, { auth: makeAuth({ permissions: ['users:read'] }) });

    const heading = await screen.findByRole('heading', { name: /^overview$/i });
    expect(heading).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /users/i })).toHaveAttribute('href', '/users');
    expect(screen.queryByRole('link', { name: /applications/i })).not.toBeInTheDocument();
  });
});
