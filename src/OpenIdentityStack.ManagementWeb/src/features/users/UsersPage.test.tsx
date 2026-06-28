import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, page, resetApiMocks } from '@/test/mock-api';
import { UsersPage } from './UsersPage';

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
  mockApi.users.getUsers.mockResolvedValue(
    page([{ id: 'u1', email: 'ada@northwind.io', displayName: 'Ada Lovelace', status: 'Active', createdAt: '2026-06-01T00:00:00Z' }])
  );
});

describe('UsersPage', () => {
  it('renders users returned by the API', async () => {
    renderManagementWeb(<UsersPage />, { auth: makeAuth() });

    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
    expect(screen.getByText('ada@northwind.io')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('shows the Add user action for operators with write access', async () => {
    renderManagementWeb(<UsersPage />, { auth: makeAuth({ permissions: ['users:read', 'users:write'] }) });

    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add user/i })).toBeInTheDocument();
  });

  it('hides the Add user action for read-only operators', async () => {
    renderManagementWeb(<UsersPage />, { auth: makeAuth({ permissions: ['users:read'] }) });

    expect(await screen.findByText('Ada Lovelace')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add user/i })).not.toBeInTheDocument();
  });

  it('renders an error state when the request fails', async () => {
    mockApi.users.getUsers.mockRejectedValue(new Error('Boom'));
    renderManagementWeb(<UsersPage />, { auth: makeAuth() });

    expect(await screen.findByText('Boom')).toBeInTheDocument();
  });
});
