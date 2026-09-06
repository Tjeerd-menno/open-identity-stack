import { screen, waitFor, within } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, resetApiMocks } from '@/test/mock-api';
import { AppRoutes } from './AppRoutes';

vi.mock('@/lib/api', async () => {
  const { mockApi } = await import('@/test/mock-api');
  return { api: mockApi, getApiErrorMessage: (error: unknown) => String(error) };
});
beforeEach(resetApiMocks);

it.each([
  ['sessions:revoke'],
  ['sessions:revoke', 'users:read'],
  ['sessions:revoke', 'applications:read'],
  ['users:read', 'applications:read'],
])('denies the cutover route and hides navigation without every prerequisite (%j)', async (...permissions: string[]) => {
  renderManagementWeb(<AppRoutes />, { auth: makeAuth({ permissions }), initialEntries: ['/security/cutover'] });
  expect(await screen.findByText('Access denied')).toBeInTheDocument();
  const nav = screen.getByRole('navigation', { name: /management navigation/i });
  expect(within(nav).queryByRole('link', { name: 'Credential cutover' })).not.toBeInTheDocument();
  expect(mockApi.cutover.getReadiness).not.toHaveBeenCalled();
});

it.each([
  ['sessions:revoke', 'users:read', 'applications:read'],
  ['*'],
])('allows cutover navigation and readiness with all required authority (%j)', async (...permissions: string[]) => {
  mockApi.cutover.getReadiness.mockRejectedValue(new Error('test readiness unavailable'));
  renderManagementWeb(<AppRoutes />, { auth: makeAuth({ permissions }), initialEntries: ['/security/cutover'] });
  const nav = screen.getByRole('navigation', { name: /management navigation/i });
  expect(within(nav).getByRole('link', { name: 'Credential cutover' })).toBeInTheDocument();
  await waitFor(() => expect(mockApi.cutover.getReadiness).toHaveBeenCalledTimes(1));
  expect(screen.queryByText('Access denied')).not.toBeInTheDocument();
});
