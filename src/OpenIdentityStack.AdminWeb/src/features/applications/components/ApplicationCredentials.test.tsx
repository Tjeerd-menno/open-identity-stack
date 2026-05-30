import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApplicationCredentials } from './ApplicationCredentials';
import { AddApplicationCertificateDialog } from './AddApplicationCertificateDialog';
import { AddApplicationSecretDialog } from './AddApplicationSecretDialog';
import { ApplicationSecretDisplay } from './ApplicationSecretDisplay';
import { ApplicationClientType, ApplicationProfile, type Application } from '@/types';

const { hooks } = vi.hoisted(() => ({
  hooks: {
    useApplicationCredentials: vi.fn(),
    useAddApplicationSecret: vi.fn(),
    useAddApplicationCertificate: vi.fn(),
    useRevokeApplicationCredential: vi.fn(),
  },
}));

vi.mock('../hooks/useApplicationCredentials', () => ({
  useApplicationCredentials: hooks.useApplicationCredentials,
}));

vi.mock('../hooks/useAddApplicationSecret', () => ({
  useAddApplicationSecret: hooks.useAddApplicationSecret,
}));

vi.mock('../hooks/useAddApplicationCertificate', () => ({
  useAddApplicationCertificate: hooks.useAddApplicationCertificate,
}));

vi.mock('../hooks/useRevokeApplicationCredential', () => ({
  useRevokeApplicationCredential: hooks.useRevokeApplicationCredential,
}));

const confidentialApplication: Application = {
  id: 'app-1',
  clientId: 'orders-worker',
  displayName: 'Orders Worker',
  description: 'Background worker',
  profile: ApplicationProfile.MachineToMachine,
  clientType: ApplicationClientType.Confidential,
  status: 'Active',
  redirectUris: [],
  postLogoutRedirectUris: [],
  allowedScopes: ['api'],
  allowedGrantTypes: ['client_credentials'],
  requirePkce: false,
  requireConsent: false,
  credentialCount: 1,
  certificateCount: 1,
  requiresMigrationReview: false,
  migrationSource: null,
  createdAt: '2026-05-24T12:00:00Z',
  modifiedAt: null,
};

const publicApplication: Application = {
  ...confidentialApplication,
  id: 'app-public',
  clientId: 'orders-spa',
  displayName: 'Orders SPA',
  profile: ApplicationProfile.SinglePage,
  clientType: ApplicationClientType.Public,
  allowedGrantTypes: ['authorization_code'],
  credentialCount: 0,
  certificateCount: 0,
};

describe('Application credential management components', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    hooks.useApplicationCredentials.mockReturnValue({
      data: [],
      isLoading: false,
    });
    hooks.useAddApplicationSecret.mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: false,
    });
    hooks.useAddApplicationCertificate.mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: false,
    });
    hooks.useRevokeApplicationCredential.mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: false,
    });
  });

  it('renders credential metadata and management actions for confidential applications', () => {
    hooks.useApplicationCredentials.mockReturnValue({
      data: [
        {
          id: 'cred-secret',
          applicationId: 'app-1',
          type: 'ClientSecret',
          thumbprint: null,
          subject: null,
          description: 'Primary secret',
          expiresAt: '2026-06-24T12:00:00Z',
          createdAt: '2026-05-24T12:00:00Z',
          lastUsedAt: null,
          revokedAt: null,
        },
        {
          id: 'cred-cert',
          applicationId: 'app-1',
          type: 'X509Certificate',
          thumbprint: 'ABC123',
          subject: 'CN=worker.example.com',
          description: 'Worker certificate',
          expiresAt: null,
          createdAt: '2026-05-24T12:00:00Z',
          lastUsedAt: '2026-05-24T12:30:00Z',
          revokedAt: null,
        },
      ],
      isLoading: false,
    });

    render(<ApplicationCredentials application={confidentialApplication} />);

    expect(screen.getByRole('heading', { name: 'Credentials' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add secret/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add certificate/i })).toBeInTheDocument();
    expect(screen.getByText('Primary secret')).toBeInTheDocument();
    expect(screen.getByText('Worker certificate')).toBeInTheDocument();
    expect(screen.getByText('ABC123')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /revoke/i })).toHaveLength(2);
  });

  it('blocks credential management for public applications with an explanatory message', () => {
    render(<ApplicationCredentials application={publicApplication} />);

    expect(screen.getByText('Public applications cannot use client secrets or certificates.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add secret/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add certificate/i })).not.toBeInTheDocument();
  });

  it('adds a secret and displays the one-time client secret response', async () => {
    const user = userEvent.setup();
    const addSecret = vi.fn().mockResolvedValue({
      credentialId: 'cred-secret',
      clientSecret: 'one-time-secret',
    });
    hooks.useAddApplicationSecret.mockReturnValue({
      mutateAsync: addSecret,
      isPending: false,
    });

    render(
      <AddApplicationSecretDialog
        open
        onOpenChange={vi.fn()}
        applicationId="app-1"
      />
    );

    await user.type(screen.getByLabelText(/description/i), 'Rotation secret');
    await user.type(screen.getByLabelText(/expires at/i), '2026-06-24T12:00');
    await user.click(screen.getByRole('checkbox', { name: /revoke existing/i }));
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /add secret/i }));
    });

    expect(addSecret).toHaveBeenCalledWith({
      applicationId: 'app-1',
      data: {
        description: 'Rotation secret',
        expiresAt: '2026-06-24T12:00',
        revokeExisting: true,
      },
    });
    expect(screen.getByText('Client secret created')).toBeInTheDocument();
    expect(screen.getByText('one-time-secret')).toBeInTheDocument();
    expect(screen.getByText(/you will not be able to see this secret again/i)).toBeInTheDocument();
  });

  it('adds a certificate credential from certificate metadata', async () => {
    const user = userEvent.setup();
    const addCertificate = vi.fn().mockResolvedValue({ credentialId: 'cred-cert' });
    const onOpenChange = vi.fn();
    hooks.useAddApplicationCertificate.mockReturnValue({
      mutateAsync: addCertificate,
      isPending: false,
    });

    render(
      <AddApplicationCertificateDialog
        open
        onOpenChange={onOpenChange}
        applicationId="app-1"
      />
    );

    await user.type(screen.getByLabelText(/thumbprint/i), 'ABC123');
    await user.type(screen.getByLabelText(/subject/i), 'CN=worker.example.com');
    await user.type(screen.getByLabelText(/description/i), 'Worker certificate');
    await user.type(screen.getByLabelText(/expires at/i), '2027-05-24T12:00');
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /add certificate/i }));
    });

    expect(addCertificate).toHaveBeenCalledWith({
      applicationId: 'app-1',
      data: {
        thumbprint: 'ABC123',
        subject: 'CN=worker.example.com',
        description: 'Worker certificate',
        expiresAt: '2027-05-24T12:00',
      },
    });
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('keeps the one-time secret visible with copy affordance and no stored hash', () => {
    render(<ApplicationSecretDisplay clientSecret="one-time-secret" />);

    expect(screen.getByText('one-time-secret')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /copy client secret/i })).toBeInTheDocument();
    expect(screen.getByText(/store it securely now/i)).toBeInTheDocument();
    expect(screen.queryByText(/hash/i)).not.toBeInTheDocument();
  });
});
