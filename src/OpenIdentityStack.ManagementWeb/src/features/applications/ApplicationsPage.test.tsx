import { screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { makeAuth, renderManagementWeb } from '@/test/render';
import { mockApi, page, resetApiMocks } from '@/test/mock-api';
import { ApplicationsPage } from './ApplicationsPage';

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
  mockApi.applications.getApplications.mockResolvedValue(
    page([
      {
        id: 'a1',
        clientId: 'northwind-web',
        displayName: 'Northwind Web',
        profile: 'Web',
        clientType: 'Confidential',
        status: 'Active',
        allowedGrantTypes: ['authorization_code'],
        credentialCount: 1,
        createdAt: '2026-06-01T00:00:00Z',
        modifiedAt: null,
      },
    ])
  );
});

describe('ApplicationsPage', () => {
  it('renders applications with the client id and profile', async () => {
    renderManagementWeb(<ApplicationsPage />, { auth: makeAuth() });

    expect(await screen.findByText('Northwind Web')).toBeInTheDocument();
    expect(screen.getByText('northwind-web')).toBeInTheDocument();
    expect(screen.getByText('Confidential')).toBeInTheDocument();
  });

  it('exposes profile and status filters', async () => {
    renderManagementWeb(<ApplicationsPage />, { auth: makeAuth() });

    expect(await screen.findByText('Northwind Web')).toBeInTheDocument();
    expect(screen.getByLabelText('Profile')).toBeInTheDocument();
    expect(screen.getByLabelText('Status')).toBeInTheDocument();
  });

  it('hides Create application for read-only operators', async () => {
    renderManagementWeb(<ApplicationsPage />, { auth: makeAuth({ permissions: ['applications:read'] }) });

    expect(await screen.findByText('Northwind Web')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /create application/i })).not.toBeInTheDocument();
  });
});
