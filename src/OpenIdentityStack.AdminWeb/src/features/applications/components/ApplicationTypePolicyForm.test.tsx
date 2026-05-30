import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApplicationForm } from './ApplicationForm';
import {
  ApplicationClientType,
  ApplicationOptionAvailability,
  ApplicationProfile,
  type ApplicationProfilePolicy,
} from '@/types';

const { hooks } = vi.hoisted(() => ({
  hooks: {
    useApplicationProfilePolicies: vi.fn(),
  },
}));

vi.mock('../hooks/useApplicationProfilePolicies', () => ({
  useApplicationProfilePolicies: hooks.useApplicationProfilePolicies,
}));

const policies: ApplicationProfilePolicy[] = [
  {
    applicationProfile: ApplicationProfile.Web,
    isSelectable: true,
    unavailabilityReason: null,
    defaultClientProfile: ApplicationClientType.Confidential,
    allowedClientProfiles: [ApplicationClientType.Confidential],
    allowedGrantTypes: ['authorization_code', 'refresh_token'],
    defaultGrantTypes: ['authorization_code'],
    options: {
      clientProfile: ApplicationOptionAvailability.ReadOnly,
      clientSecrets: ApplicationOptionAvailability.Available,
      redirectUris: ApplicationOptionAvailability.Available,
      postLogoutRedirectUris: ApplicationOptionAvailability.Available,
      pkce: ApplicationOptionAvailability.Available,
      consent: ApplicationOptionAvailability.Available,
      refreshTokenGrant: ApplicationOptionAvailability.Available,
    },
    requirePkce: false,
    defaultRequirePkce: true,
    defaultRequireConsent: true,
    requiresRedirectUris: true,
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
      clientProfile: ApplicationOptionAvailability.ReadOnly,
      clientSecrets: ApplicationOptionAvailability.Hidden,
      redirectUris: ApplicationOptionAvailability.Available,
      postLogoutRedirectUris: ApplicationOptionAvailability.Available,
      pkce: ApplicationOptionAvailability.ReadOnly,
      consent: ApplicationOptionAvailability.Available,
      refreshTokenGrant: ApplicationOptionAvailability.Available,
      allowedWebOrigins: ApplicationOptionAvailability.Available,
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
      clientProfile: ApplicationOptionAvailability.ReadOnly,
      clientSecrets: ApplicationOptionAvailability.Hidden,
      redirectUris: ApplicationOptionAvailability.Available,
      postLogoutRedirectUris: ApplicationOptionAvailability.Available,
      pkce: ApplicationOptionAvailability.ReadOnly,
      consent: ApplicationOptionAvailability.Available,
      nativeRedirectUriPatterns: ApplicationOptionAvailability.Available,
      loopbackRedirectUri: ApplicationOptionAvailability.Available,
      refreshTokenGrant: ApplicationOptionAvailability.Available,
    },
    requirePkce: true,
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
      clientProfile: ApplicationOptionAvailability.ReadOnly,
      clientSecrets: ApplicationOptionAvailability.Available,
      redirectUris: ApplicationOptionAvailability.Hidden,
      postLogoutRedirectUris: ApplicationOptionAvailability.Hidden,
      pkce: ApplicationOptionAvailability.Hidden,
      consent: ApplicationOptionAvailability.Hidden,
      clientCredentialsGrant: ApplicationOptionAvailability.Available,
    },
    requirePkce: false,
    defaultRequirePkce: false,
    defaultRequireConsent: false,
    requiresRedirectUris: false,
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
      clientProfile: ApplicationOptionAvailability.ReadOnly,
      deviceCodeGrant: ApplicationOptionAvailability.ReadOnly,
    },
    requirePkce: false,
    defaultRequirePkce: false,
    defaultRequireConsent: true,
    requiresRedirectUris: false,
  },
];

describe('ApplicationForm policy railroading', () => {
  beforeEach(() => {
    hooks.useApplicationProfilePolicies.mockReturnValue({
      data: policies,
      isLoading: false,
      isError: false,
    });
  });

  it('applies web defaults from the selected policy', async () => {
    render(<ApplicationForm onSubmit={vi.fn()} />);

    expect(screen.getByText('Client Profile')).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByDisplayValue('Confidential')).toBeInTheDocument();
      expect(screen.getByText('authorization_code')).toBeInTheDocument();
      expect(screen.getByRole('checkbox', { name: /refresh_token/i })).toBeInTheDocument();
      expect(screen.getByRole('checkbox', { name: /require pkce/i })).toBeChecked();
    });
  });

  it('railroads machine-to-machine applications to client_credentials with no redirect or consent controls', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    render(<ApplicationForm onSubmit={onSubmit} initialProfile={ApplicationProfile.MachineToMachine} />);

    await waitFor(() => {
      expect(screen.queryByText('Redirect URIs')).not.toBeInTheDocument();
    });

    await user.type(screen.getByLabelText(/client id/i), 'orders-worker');
    await user.type(screen.getByLabelText(/display name/i), 'Orders Worker');
    await user.click(screen.getByRole('checkbox', { name: 'api' }));
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: /create application/i }));
    });

    expect(screen.queryByText('Redirect URIs')).not.toBeInTheDocument();
    expect(screen.queryByText('Post Logout Redirect URIs')).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: /require consent/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: /require pkce/i })).not.toBeInTheDocument();
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
      profile: ApplicationProfile.MachineToMachine,
      clientType: ApplicationClientType.Confidential,
      allowedGrantTypes: ['client_credentials'],
      redirectUris: [],
      postLogoutRedirectUris: [],
      requirePkce: false,
      requireConsent: false,
    }));
  });

  it('shows native redirect guidance and reserved device messaging', async () => {
    render(<ApplicationForm onSubmit={vi.fn()} initialProfile={ApplicationProfile.Native} />);

    await waitFor(() => {
      expect(screen.getByText(/device applications are reserved until the device authorization flow is implemented and tested/i)).toBeInTheDocument();
      expect(screen.getByText(/claimed https redirects are preferred/i)).toBeInTheDocument();
      expect(screen.getByText(/private scheme and loopback redirects are supported/i)).toBeInTheDocument();
    });
  });
});
