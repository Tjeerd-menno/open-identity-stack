import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import {
  ApplicationClientType,
  ApplicationOptionAvailability,
  ApplicationProfile,
  type Application,
  type ApplicationProfilePolicy,
} from './applications-api';
import { ApplicationForm } from './ApplicationForm';

const apiBase = 'http://localhost:5000';
const originalFetch = globalThis.fetch;

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
      redirectUris: ApplicationOptionAvailability.Available,
      postLogoutRedirectUris: ApplicationOptionAvailability.Available,
      pkce: ApplicationOptionAvailability.Available,
      consent: ApplicationOptionAvailability.Available,
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
      redirectUris: ApplicationOptionAvailability.Hidden,
      postLogoutRedirectUris: ApplicationOptionAvailability.Hidden,
      pkce: ApplicationOptionAvailability.Hidden,
      consent: ApplicationOptionAvailability.Hidden,
    },
    requirePkce: false,
    defaultRequirePkce: false,
    defaultRequireConsent: false,
    requiresRedirectUris: false,
  },
  {
    applicationProfile: ApplicationProfile.Device,
    isSelectable: false,
    unavailabilityReason: 'Device applications are reserved.',
    defaultClientProfile: ApplicationClientType.Public,
    allowedClientProfiles: [ApplicationClientType.Public],
    allowedGrantTypes: ['urn:ietf:params:oauth:grant-type:device_code'],
    defaultGrantTypes: ['urn:ietf:params:oauth:grant-type:device_code'],
    options: {},
    requirePkce: false,
    defaultRequirePkce: false,
    defaultRequireConsent: true,
    requiresRedirectUris: false,
  },
];

function stubPolicies() {
  vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => {
    if (input.toString() === `${apiBase}/api/admin/applications/policies/profiles`) {
      return Promise.resolve(new Response(JSON.stringify(policies), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }));
    }

    return Promise.resolve(new Response(null, { status: 404 }));
  }));
}

afterEach(() => {
  globalThis.fetch = originalFetch;
});

describe('ApplicationForm', () => {
  it('applies policy defaults and submits machine-to-machine applications without redirect controls', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    stubPolicies();

    renderManagementWeb(<ApplicationForm initialProfile={ApplicationProfile.MachineToMachine} onSubmit={onSubmit} />);

    expect(await screen.findByText(/machine-to-machine applications use only the client credentials grant/i)).toBeInTheDocument();
    expect(screen.queryByText(/redirect uris/i)).not.toBeInTheDocument();

    await user.type(screen.getByLabelText(/client id/i), 'orders-worker');
    await user.type(screen.getByLabelText(/display name/i), 'Orders Worker');
    await user.click(screen.getByRole('checkbox', { name: 'api' }));
    await user.click(screen.getByRole('button', { name: /create application/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
        clientId: 'orders-worker',
        displayName: 'Orders Worker',
        profile: ApplicationProfile.MachineToMachine,
        clientType: ApplicationClientType.Confidential,
        allowedGrantTypes: ['client_credentials'],
        allowedScopes: ['api'],
        redirectUris: [],
        postLogoutRedirectUris: [],
        requirePkce: false,
        requireConsent: false,
      }));
    });
  });

  it('submits metadata updates for existing applications', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);
    const application: Application = {
      id: 'app-1',
      clientId: 'orders-web',
      displayName: 'Orders Web',
      description: 'Old description',
      profile: ApplicationProfile.Web,
      clientType: ApplicationClientType.Confidential,
      status: 'Active',
      redirectUris: [],
      postLogoutRedirectUris: [],
      allowedScopes: [],
      allowedGrantTypes: [],
      requirePkce: true,
      requireConsent: true,
      credentialCount: 0,
      certificateCount: 0,
      requiresMigrationReview: false,
      migrationSource: null,
      createdAt: '2026-01-01T00:00:00Z',
      modifiedAt: null,
    };

    renderManagementWeb(<ApplicationForm application={application} onSubmit={onSubmit} />);

    await user.clear(screen.getByLabelText(/display name/i));
    await user.type(screen.getByLabelText(/display name/i), 'Orders Admin');
    await user.clear(screen.getByLabelText(/description/i));
    await user.type(screen.getByLabelText(/description/i), 'New description');
    await user.click(screen.getByRole('button', { name: /update application/i }));

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({
        displayName: 'Orders Admin',
        description: 'New description',
      });
    });
  });
});
