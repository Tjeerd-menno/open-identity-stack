import { screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, page, resetApiMocks } from '@/test/mock-api';
import { SessionsPage } from './SessionsPage';

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
  mockApi.sessions.getSessions.mockResolvedValue(
    page([
      {
        id: 's1',
        userId: 'u1',
        ipAddress: '192.0.2.14',
        userAgent: 'Mozilla/5.0 (Macintosh) Chrome/124.0 Safari/537.36',
        status: 'Active',
        clientCount: 1,
        lastActivityAt: '2026-06-28T08:00:00Z',
        expiresAt: '2026-07-05T08:00:00Z',
        createdAt: '2026-06-28T07:00:00Z',
      },
    ])
  );
});

describe('SessionsPage', () => {
  it('renders sessions with a derived device label and IP', async () => {
    renderManagementWeb(<SessionsPage />, { auth: makeAuth() });

    expect(await screen.findByText('192.0.2.14')).toBeInTheDocument();
    expect(screen.getByText('Browser')).toBeInTheDocument();
    // "Active" also appears as a status-filter option, so scope to the table row.
    const row = screen.getByText('192.0.2.14').closest('tr');
    expect(row).not.toBeNull();
    expect(within(row as HTMLElement).getByText('Active')).toBeInTheDocument();
  });

  it('offers a status filter', async () => {
    renderManagementWeb(<SessionsPage />, { auth: makeAuth() });

    expect(await screen.findByText('192.0.2.14')).toBeInTheDocument();
    expect(screen.getByLabelText('Status')).toBeInTheDocument();
  });
});
