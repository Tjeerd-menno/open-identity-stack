import { screen, within } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, page, resetApiMocks } from '@/test/mock-api';
import { AppRoutes } from './AppRoutes';

vi.mock('@/lib/api', async () => {
  const { mockApi } = await import('@/test/mock-api');
  return { api: mockApi, getApiErrorMessage: (error: unknown) => String(error) };
});
beforeEach(() => {
  resetApiMocks();
  mockApi.users.getUsers.mockResolvedValue(page([]));
  mockApi.applications.getApplications.mockResolvedValue(page([]));
  mockApi.sessions.getSessions.mockResolvedValue(page([]));
  mockApi.providers.getProviders.mockResolvedValue([]);
  mockApi.audit.getAuditEntries.mockResolvedValue(page([]));
});

it.each([
  ['sessions:revoke', 'users:read', 'applications:read'],
  ['*'],
])('redirects the removed cutover route to overview without navigation (%j)', async (...permissions: string[]) => {
  renderManagementWeb(<AppRoutes />, { auth: makeAuth({ permissions }), initialEntries: ['/security/cutover'] });
  expect(await screen.findByRole('heading', { name: 'Overview' })).toBeInTheDocument();
  const nav = screen.getByRole('navigation', { name: /management navigation/i });
  expect(within(nav).queryByRole('link', { name: 'Credential cutover' })).not.toBeInTheDocument();
  expect(within(nav).getByRole('link', { name: 'Users' })).toHaveAttribute('href', '/users');
  expect(within(nav).getByRole('link', { name: 'Applications' })).toHaveAttribute('href', '/applications');
});
