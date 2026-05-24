import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { ApplicationDetail } from './ApplicationDetail';
import { ApplicationForm } from './ApplicationForm';
import { ApplicationList } from './ApplicationList';
import { ApplicationStatusBadge } from './ApplicationStatusBadge';
import { ApplicationClientType, ApplicationType } from '@/types';

const { navigate, hooks } = vi.hoisted(() => ({
  navigate: vi.fn(),
  hooks: {
    useApplications: vi.fn(),
    useApplication: vi.fn(),
    useApplicationCredentials: vi.fn(),
    useAddApplicationCertificate: vi.fn(),
    useAddApplicationSecret: vi.fn(),
    useDeleteApplication: vi.fn(),
    useRevokeApplicationCredential: vi.fn(),
  },
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    useNavigate: () => navigate,
    useParams: () => ({ id: 'app-1' }),
  };
});

vi.mock('../hooks/useApplications', () => ({
  useApplications: hooks.useApplications,
}));

vi.mock('../hooks/useApplication', () => ({
  useApplication: hooks.useApplication,
}));

vi.mock('../hooks/useApplicationCredentials', () => ({
  useApplicationCredentials: hooks.useApplicationCredentials,
}));

vi.mock('../hooks/useAddApplicationCertificate', () => ({
  useAddApplicationCertificate: hooks.useAddApplicationCertificate,
}));

vi.mock('../hooks/useAddApplicationSecret', () => ({
  useAddApplicationSecret: hooks.useAddApplicationSecret,
}));

vi.mock('../hooks/useDeleteApplication', () => ({
  useDeleteApplication: hooks.useDeleteApplication,
}));

vi.mock('../hooks/useRevokeApplicationCredential', () => ({
  useRevokeApplicationCredential: hooks.useRevokeApplicationCredential,
}));

const application = {
  id: 'app-1',
  clientId: 'orders-web',
  displayName: 'Orders Web',
  description: 'Orders UI',
  type: ApplicationType.Web,
  clientType: ApplicationClientType.Confidential,
  status: 'Active' as const,
  redirectUris: ['https://orders.example.com/callback'],
  postLogoutRedirectUris: [],
  allowedScopes: ['openid', 'orders.read'],
  allowedGrantTypes: ['authorization_code'],
  requirePkce: true,
  requireConsent: true,
  credentialCount: 0,
  certificateCount: 0,
  requiresMigrationReview: false,
  migrationSource: null,
  createdAt: '2026-05-24T12:00:00Z',
  modifiedAt: null,
};

describe('Application management components', () => {
  it.each(['Active', 'Disabled'] as const)('renders application status %s', (status) => {
    render(<ApplicationStatusBadge status={status} />);

    expect(screen.getByText(status)).toHaveAttribute('data-status', status);
  });

  it('renders application list with application terminology and row actions', () => {
    hooks.useApplications.mockReturnValue({
      data: {
        items: [{
          id: application.id,
          clientId: application.clientId,
          displayName: application.displayName,
          type: application.type,
          clientType: application.clientType,
          status: application.status,
          allowedGrantTypes: application.allowedGrantTypes,
          credentialCount: 0,
          createdAt: application.createdAt,
          modifiedAt: null,
        }],
        totalCount: 1,
        page: 1,
        pageSize: 20,
      },
      isLoading: false,
    });
    hooks.useDeleteApplication.mockReturnValue({ mutateAsync: vi.fn() });

    render(<ApplicationList />, { wrapper: MemoryRouter });

    expect(screen.getByRole('heading', { name: 'Applications' })).toBeInTheDocument();
    expect(screen.getByText('orders-web')).toBeInTheDocument();
    expect(screen.getByText('Orders Web')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /new application/i })).toBeInTheDocument();
  });

  it('submits new application configuration', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<ApplicationForm onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText(/client id/i), 'orders-web');
    await user.type(screen.getByLabelText(/display name/i), 'Orders Web');
    await user.click(screen.getByRole('button', { name: /add redirect uri/i }));
    await user.type(screen.getByPlaceholderText('https://example.com/callback'), 'https://orders.example.com/callback');
    await user.click(screen.getByRole('checkbox', { name: 'openid' }));
    await user.click(screen.getByRole('checkbox', { name: 'authorization_code' }));
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /create application/i }));
    });

    expect(onSubmit).toHaveBeenCalled();
    expect(onSubmit.mock.calls[0][0]).toEqual(expect.objectContaining({
      clientId: 'orders-web',
      displayName: 'Orders Web',
      type: ApplicationType.Web,
      clientType: ApplicationClientType.Confidential,
      allowedScopes: ['openid'],
      allowedGrantTypes: ['authorization_code'],
      redirectUris: ['https://orders.example.com/callback'],
    }));
  });

  it('renders application detail baseline information', () => {
    hooks.useApplication.mockReturnValue({ data: application, isLoading: false });
    hooks.useApplicationCredentials.mockReturnValue({ data: [], isLoading: false });
    hooks.useAddApplicationCertificate.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useAddApplicationSecret.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useDeleteApplication.mockReturnValue({ mutateAsync: vi.fn() });
    hooks.useRevokeApplicationCredential.mockReturnValue({ mutateAsync: vi.fn() });

    render(<ApplicationDetail />, { wrapper: MemoryRouter });

    expect(screen.getByRole('heading', { name: 'Orders Web' })).toBeInTheDocument();
    expect(screen.getByText('orders-web')).toBeInTheDocument();
    expect(screen.getByText('Allowed Grant Types')).toBeInTheDocument();
    expect(screen.getByText('authorization_code')).toBeInTheDocument();
  });

  it('renders OAuth configuration and credential management tabs on application detail', () => {
    hooks.useApplication.mockReturnValue({
      data: {
        ...application,
        type: ApplicationType.MachineToMachine,
        clientType: ApplicationClientType.Confidential,
        allowedGrantTypes: ['client_credentials'],
      },
      isLoading: false,
    });
    hooks.useApplicationCredentials.mockReturnValue({ data: [], isLoading: false });
    hooks.useAddApplicationCertificate.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useAddApplicationSecret.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useDeleteApplication.mockReturnValue({ mutateAsync: vi.fn() });
    hooks.useRevokeApplicationCredential.mockReturnValue({ mutateAsync: vi.fn() });

    render(<ApplicationDetail />, { wrapper: MemoryRouter });

    expect(screen.getByRole('tab', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /oauth configuration/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /credentials/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Credentials' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add secret/i })).toBeInTheDocument();
  });
});
