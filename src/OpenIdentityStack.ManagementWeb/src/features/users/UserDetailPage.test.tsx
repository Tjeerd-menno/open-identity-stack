import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Route, Routes } from 'react-router';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, page, resetApiMocks } from '@/test/mock-api';
import { UserDetailPage } from './UserDetailPage';

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
  mockApi.users.getUser.mockResolvedValue({
    id: 'u1',
    email: 'ada@northwind.io',
    displayName: 'Ada Lovelace',
    status: 'Active',
    createdAt: '2026-06-01T00:00:00Z',
    mfaEnabled: false,
    lastLoginAt: null,
    modifiedAt: null,
    profile: {},
  });
  mockApi.users.getUserRoles.mockResolvedValue([]);
  mockApi.users.getUserGroups.mockResolvedValue([]);
  mockApi.users.getUserUpstreamIdentities.mockResolvedValue([]);
  mockApi.roles.getRoles.mockResolvedValue(page([]));
  mockApi.providers.getProviders.mockResolvedValue([]);
});

function renderDetail(auth = makeAuth()) {
  return renderManagementWeb(
    <Routes>
      <Route path="/users/:userId" element={<UserDetailPage />} />
    </Routes>,
    { auth, initialEntries: ['/users/u1'] }
  );
}

describe('UserDetailPage', () => {
  it('renders the user header and profile fields', async () => {
    renderDetail();

    expect(await screen.findByRole('heading', { name: 'Ada Lovelace' })).toBeInTheDocument();
    expect(screen.getAllByText('ada@northwind.io').length).toBeGreaterThan(0);
    expect(screen.getByRole('tab', { name: /profile/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /roles/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /upstream identities/i })).toBeInTheDocument();
  });

  it('offers reset and disable actions gated on their granular permissions', async () => {
    renderDetail(makeAuth({ permissions: ['users:read', 'users:reset-password', 'users:disable'] }));

    expect(await screen.findByRole('button', { name: /reset password/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /disable user/i })).toBeInTheDocument();
  });

  it('does not show reset/disable to a write-only operator lacking the granular grants', async () => {
    renderDetail(makeAuth({ permissions: ['users:read', 'users:write'] }));

    expect(await screen.findByRole('heading', { name: 'Ada Lovelace' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /reset password/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /disable user/i })).not.toBeInTheDocument();
  });

  it('hides write actions for read-only operators', async () => {
    renderDetail(makeAuth({ permissions: ['users:read'] }));

    expect(await screen.findByRole('heading', { name: 'Ada Lovelace' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /reset password/i })).not.toBeInTheDocument();
  });
});
