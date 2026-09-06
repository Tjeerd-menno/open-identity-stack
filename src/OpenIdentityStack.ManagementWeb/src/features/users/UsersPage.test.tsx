import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
  it('blocks deletion while quarantined evidence must be retained', async () => {
    mockApi.users.getUserUpstreamIdentities.mockResolvedValue([{ providerId: 'p1', subject: 'legacy', isQuarantined: true }]);
    const user = userEvent.setup();
    renderManagementWeb(<UsersPage />, { auth: makeAuth() });
    await screen.findByText('Ada Lovelace');
    await user.click(screen.getByRole('button', { name: 'Row actions' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Delete user' }));

    expect(await screen.findByText(/quarantined identity evidence must be retained/i)).toBeInTheDocument();
    const confirm = within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete user' });
    expect(confirm).toBeDisabled();
    await user.click(confirm);
    expect(mockApi.users.deleteUser).not.toHaveBeenCalled();
  });

  it('blocks deletion if the identity retention check fails', async () => {
    mockApi.users.getUserUpstreamIdentities.mockRejectedValue(new Error('Unavailable'));
    const user = userEvent.setup();
    renderManagementWeb(<UsersPage />, { auth: makeAuth() });
    await screen.findByText('Ada Lovelace');
    await user.click(screen.getByRole('button', { name: 'Row actions' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Delete user' }));

    expect(await screen.findByText(/unable to verify identity retention/i)).toBeInTheDocument();
    expect(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete user' })).toBeDisabled();
    expect(mockApi.users.deleteUser).not.toHaveBeenCalled();
  });

  it('waits for the retention check before allowing ordinary deletion', async () => {
    let resolveIdentities!: (value: []) => void;
    mockApi.users.getUserUpstreamIdentities.mockReturnValue(new Promise<[]>((resolve) => { resolveIdentities = resolve; }));
    mockApi.users.deleteUser.mockResolvedValue(undefined);
    const user = userEvent.setup();
    renderManagementWeb(<UsersPage />, { auth: makeAuth() });
    await screen.findByText('Ada Lovelace');
    await user.click(screen.getByRole('button', { name: 'Row actions' }));
    await user.click(await screen.findByRole('menuitem', { name: 'Delete user' }));
    const confirm = within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete user' });
    expect(confirm).toBeDisabled();
    resolveIdentities([]);
    await waitFor(() => expect(confirm).toBeEnabled());
    await user.click(confirm);
    await waitFor(() => expect(mockApi.users.deleteUser).toHaveBeenCalledWith('u1'));
  });

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
