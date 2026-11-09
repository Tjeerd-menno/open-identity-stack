import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router';
import { beforeEach, expect, it, vi } from 'vitest';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, page, resetApiMocks } from '@/test/mock-api';
import { GroupDetailPage } from './GroupDetailPage';

vi.mock('@/lib/api', async () => ({
  api: (await import('@/test/mock-api')).mockApi,
  getApiErrorMessage: (error: unknown) => (error instanceof Error ? error.message : String(error)),
}));

beforeEach(() => {
  resetApiMocks();
  mockApi.groups.getGroup.mockResolvedValue({
    id: 'g1', name: 'Engineering', description: null, createdAt: '2026-06-01T00:00:00Z',
  });
  mockApi.groups.getGroupMembers.mockResolvedValue(page([]));
  mockApi.groups.getGroupMappings.mockResolvedValue(page([]));
});

it('maps a group to a role beyond the first page of available roles', async () => {
  const user = userEvent.setup();
  const roles = Array.from({ length: 101 }, (_, index) => ({
    id: `role-${index}`, name: `role-${index}`, displayName: `Role ${index}`,
    isSystemRole: false, isActive: true, permissions: [],
  }));
  mockApi.roles.getRoles.mockImplementation(async ({ page: requestedPage }) =>
    page(roles.slice((requestedPage - 1) * 100, requestedPage * 100), {
      page: requestedPage, pageSize: 100, totalCount: 101, totalPages: 2,
    })
  );
  mockApi.groups.addGroupMapping.mockResolvedValue(undefined);
  renderManagementWeb(
    <Routes><Route path="/groups/:groupId" element={<GroupDetailPage />} /></Routes>,
    { auth: makeAuth(), initialEntries: ['/groups/g1'] }
  );

  await user.click(await screen.findByRole('tab', { name: /mappings/i }));
  await user.click(screen.getByRole('button', { name: 'Add mapping' }));
  await user.type(await screen.findByPlaceholderText('Select a role'), 'Role 100');
  await user.click(await screen.findByRole('option', { name: 'Role 100' }));
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Add mapping' }));

  await waitFor(() => expect(mockApi.groups.addGroupMapping).toHaveBeenCalledWith('g1', { type: 'Role', value: 'role-100' }));
});
