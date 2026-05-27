import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApplicationDetail } from './ApplicationDetail';
import { ApplicationForm } from './ApplicationForm';
import { ApplicationList } from './ApplicationList';
import { ApplicationStatusBadge } from './ApplicationStatusBadge';
import { ApplicationClientType, ApplicationProfile } from '@/types';

const { navigate, hooks } = vi.hoisted(() => ({
  navigate: vi.fn(),
  hooks: {
    useApplications: vi.fn(),
    useApplication: vi.fn(),
    useApplicationProfilePolicies: vi.fn(),
    useApplicationCredentials: vi.fn(),
    useAddApplicationCertificate: vi.fn(),
    useAddApplicationSecret: vi.fn(),
    useDeleteApplication: vi.fn(),
    useDisableApplication: vi.fn(),
    useEnableApplication: vi.fn(),
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

vi.mock('../hooks/useApplicationProfilePolicies', () => ({
  useApplicationProfilePolicies: hooks.useApplicationProfilePolicies,
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

vi.mock('../hooks/useDisableApplication', () => ({
  useDisableApplication: hooks.useDisableApplication,
}));

vi.mock('../hooks/useEnableApplication', () => ({
  useEnableApplication: hooks.useEnableApplication,
}));

vi.mock('../hooks/useRevokeApplicationCredential', () => ({
  useRevokeApplicationCredential: hooks.useRevokeApplicationCredential,
}));

const application = {
  id: 'app-1',
  clientId: 'orders-web',
  displayName: 'Orders Web',
  description: 'Orders UI',
  profile: ApplicationProfile.Web,
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

const applicationProfilePolicies = [
  {
    applicationProfile: ApplicationProfile.Web,
    isSelectable: true,
    unavailabilityReason: null,
    defaultClientProfile: ApplicationClientType.Confidential,
    allowedClientProfiles: [ApplicationClientType.Confidential],
    allowedGrantTypes: ['authorization_code', 'refresh_token'],
    defaultGrantTypes: ['authorization_code'],
    options: {
      clientProfile: 'ReadOnly',
      redirectUris: 'Available',
      postLogoutRedirectUris: 'Available',
      pkce: 'Available',
      consent: 'Available',
      refreshTokenGrant: 'Available',
    },
    requirePkce: false,
    defaultRequirePkce: true,
    defaultRequireConsent: true,
    requiresRedirectUris: true,
  },
  {
    applicationProfile: ApplicationProfile.MachineToMachine,
    isSelectable: true,
    unavailabilityReason: null,
    defaultClientProfile: ApplicationClientType.Confidential,
    allowedClientProfiles: [ApplicationClientType.Confidential],
    allowedGrantTypes: ['client_credentials'],
    defaultGrantTypes: ['client_credentials'],
    options: {
      clientProfile: 'ReadOnly',
      redirectUris: 'Hidden',
      postLogoutRedirectUris: 'Hidden',
      pkce: 'Hidden',
      consent: 'Hidden',
      clientCredentialsGrant: 'Available',
    },
    requirePkce: false,
    defaultRequirePkce: false,
    defaultRequireConsent: false,
    requiresRedirectUris: false,
  },
  {
    applicationProfile: ApplicationProfile.SinglePage,
    isSelectable: true,
    unavailabilityReason: null,
    defaultClientProfile: ApplicationClientType.Public,
    allowedClientProfiles: [ApplicationClientType.Public],
    allowedGrantTypes: ['authorization_code', 'refresh_token'],
    defaultGrantTypes: ['authorization_code'],
    options: {
      clientProfile: 'ReadOnly',
      redirectUris: 'Available',
      postLogoutRedirectUris: 'Available',
      pkce: 'ReadOnly',
      consent: 'Available',
      refreshTokenGrant: 'Available',
    },
    requirePkce: true,
    defaultRequirePkce: true,
    defaultRequireConsent: true,
    requiresRedirectUris: true,
  },
  {
    applicationProfile: ApplicationProfile.Native,
    isSelectable: true,
    unavailabilityReason: null,
    defaultClientProfile: ApplicationClientType.Public,
    allowedClientProfiles: [ApplicationClientType.Public],
    allowedGrantTypes: ['authorization_code', 'refresh_token'],
    defaultGrantTypes: ['authorization_code'],
    options: {
      clientProfile: 'ReadOnly',
      redirectUris: 'Available',
      postLogoutRedirectUris: 'Available',
      pkce: 'ReadOnly',
      consent: 'Available',
      refreshTokenGrant: 'Available',
    },
    requirePkce: true,
    defaultRequirePkce: true,
    defaultRequireConsent: true,
    requiresRedirectUris: true,
  },
  {
    applicationProfile: ApplicationProfile.Device,
    isSelectable: false,
    unavailabilityReason: 'Device applications are reserved until the device authorization flow is implemented and tested.',
    defaultClientProfile: ApplicationClientType.Public,
    allowedClientProfiles: [ApplicationClientType.Public],
    allowedGrantTypes: ['urn:ietf:params:oauth:grant-type:device_code'],
    defaultGrantTypes: ['urn:ietf:params:oauth:grant-type:device_code'],
    options: {
      clientProfile: 'ReadOnly',
      deviceCodeGrant: 'ReadOnly',
    },
    requirePkce: false,
    defaultRequirePkce: false,
    defaultRequireConsent: true,
    requiresRedirectUris: false,
  },
];

describe('Application management components', () => {
  beforeEach(() => {
    hooks.useApplicationProfilePolicies.mockReturnValue({
      data: applicationProfilePolicies,
      isLoading: false,
      isError: false,
    });
  });

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
          profile: application.profile,
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

    fireEvent.change(screen.getByLabelText(/client id/i), { target: { value: 'orders-web' } });
    fireEvent.change(screen.getByLabelText(/display name/i), { target: { value: 'Orders Web' } });
    fireEvent.change(screen.getByPlaceholderText('https://example.com/callback'), {
      target: { value: 'https://orders.example.com/callback' },
    });
    await user.click(screen.getByRole('checkbox', { name: 'openid' }));
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /create application/i }));
    });

    expect(onSubmit).toHaveBeenCalled();
    expect(onSubmit.mock.calls[0][0]).toEqual(expect.objectContaining({
      clientId: 'orders-web',
      displayName: 'Orders Web',
      profile: ApplicationProfile.Web,
      clientType: ApplicationClientType.Confidential,
      allowedScopes: ['openid'],
      allowedGrantTypes: ['authorization_code'],
      redirectUris: ['https://orders.example.com/callback'],
      requirePkce: true,
    }));
  });

  it('renders application detail baseline information', () => {
    hooks.useApplication.mockReturnValue({ data: application, isLoading: false });
    hooks.useApplicationCredentials.mockReturnValue({ data: [], isLoading: false });
    hooks.useAddApplicationCertificate.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useAddApplicationSecret.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useDeleteApplication.mockReturnValue({ mutateAsync: vi.fn() });
    hooks.useDisableApplication.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useEnableApplication.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
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
        profile: ApplicationProfile.MachineToMachine,
        clientType: ApplicationClientType.Confidential,
        allowedGrantTypes: ['client_credentials'],
      },
      isLoading: false,
    });
    hooks.useApplicationCredentials.mockReturnValue({ data: [], isLoading: false });
    hooks.useAddApplicationCertificate.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useAddApplicationSecret.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useDeleteApplication.mockReturnValue({ mutateAsync: vi.fn() });
    hooks.useDisableApplication.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useEnableApplication.mockReturnValue({ mutateAsync: vi.fn(), isPending: false });
    hooks.useRevokeApplicationCredential.mockReturnValue({ mutateAsync: vi.fn() });

    render(<ApplicationDetail />, { wrapper: MemoryRouter });

    expect(screen.getByRole('tab', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /oauth configuration/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /credentials/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Credentials' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add secret/i })).toBeInTheDocument();
  });
});
