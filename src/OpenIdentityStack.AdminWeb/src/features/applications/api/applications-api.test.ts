import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  addApplicationCertificate,
  addApplicationSecret,
  configureApplicationOAuth,
  createApplication,
  deleteApplication,
  disableApplication,
  enableApplication,
  getApplication,
  getApplicationProfilePolicies,
  getApplications,
  listApplicationCredentials,
  revokeApplicationCredential,
  updateApplicationMetadata,
} from './applications-api';
import { ApplicationClientType, ApplicationOptionAvailability, ApplicationProfile } from '@/types';

const { apiClient } = vi.hoisted(() => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock('@/lib/api/client', () => ({
  apiClient,
  default: apiClient,
}));

describe('applications API client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('lists applications with filters and pagination', async () => {
    const response = { items: [], totalCount: 0, page: 2, pageSize: 25 };
    apiClient.get.mockResolvedValueOnce(response);

    await expect(getApplications({
      page: 2,
      pageSize: 25,
      search: 'orders',
      profile: ApplicationProfile.Web,
      status: 'Active',
      clientType: ApplicationClientType.Confidential,
    })).resolves.toBe(response);

    expect(apiClient.get).toHaveBeenCalledWith('/api/admin/applications', {
      page: 2,
      pageSize: 25,
      search: 'orders',
      profile: ApplicationProfile.Web,
      status: 'Active',
      clientType: ApplicationClientType.Confidential,
    });
  });

  it('uses unified application lifecycle endpoints', async () => {
    apiClient.get.mockResolvedValueOnce({ id: 'app-1' });
    apiClient.post.mockResolvedValue({ id: 'app-1' });
    apiClient.patch.mockResolvedValueOnce({ id: 'app-1' });
    apiClient.put.mockResolvedValueOnce({ id: 'app-1' });
    apiClient.delete.mockResolvedValueOnce(undefined);

    await getApplication('app-1');
    await createApplication({
      clientId: 'orders-web',
      displayName: 'Orders Web',
      description: 'Orders UI',
      profile: ApplicationProfile.Web,
      clientType: ApplicationClientType.Confidential,
      allowedGrantTypes: ['authorization_code'],
      allowedScopes: ['openid', 'orders.read'],
      redirectUris: ['https://orders.example.com/callback'],
      postLogoutRedirectUris: [],
      requirePkce: true,
      requireConsent: true,
    });
    await updateApplicationMetadata('app-1', {
      displayName: 'Orders Admin',
      description: 'Updated',
    });
    await configureApplicationOAuth('app-1', {
      profile: ApplicationProfile.MachineToMachine,
      clientType: ApplicationClientType.Confidential,
      allowedGrantTypes: ['client_credentials'],
      allowedScopes: ['api'],
      redirectUris: [],
      postLogoutRedirectUris: [],
      requirePkce: false,
      requireConsent: false,
    });
    await disableApplication('app-1');
    await enableApplication('app-1');
    await deleteApplication('app-1');

    expect(apiClient.get).toHaveBeenCalledWith('/api/admin/applications/app-1');
    expect(apiClient.post).toHaveBeenNthCalledWith(
      1,
      '/api/admin/applications',
      expect.objectContaining({ clientId: 'orders-web' })
    );
    expect(apiClient.patch).toHaveBeenCalledWith('/api/admin/applications/app-1', {
      displayName: 'Orders Admin',
      description: 'Updated',
    });
    expect(apiClient.put).toHaveBeenCalledWith(
      '/api/admin/applications/app-1/oauth',
      expect.objectContaining({ profile: ApplicationProfile.MachineToMachine })
    );
    expect(apiClient.post).toHaveBeenNthCalledWith(2, '/api/admin/applications/app-1/disable');
    expect(apiClient.post).toHaveBeenNthCalledWith(3, '/api/admin/applications/app-1/enable');
    expect(apiClient.delete).toHaveBeenCalledWith('/api/admin/applications/app-1');
  });

  it('uses unified application credential endpoints', async () => {
    apiClient.get.mockResolvedValueOnce([]);
    apiClient.post.mockResolvedValueOnce({ credentialId: 'cred-1', clientSecret: 'secret' });
    apiClient.post.mockResolvedValueOnce({ credentialId: 'cred-2' });
    apiClient.delete.mockResolvedValueOnce(undefined);

    await listApplicationCredentials('app-1');
    await addApplicationSecret('app-1', {
      description: 'Primary secret',
      expiresAt: '2026-06-24T12:00:00Z',
      revokeExisting: true,
    });
    await addApplicationCertificate('app-1', {
      thumbprint: 'ABC123',
      subject: 'CN=worker.example.com',
      description: 'Worker certificate',
      expiresAt: '2027-05-24T12:00:00Z',
    });
    await revokeApplicationCredential('app-1', 'cred-1');

    expect(apiClient.get).toHaveBeenCalledWith('/api/admin/applications/app-1/credentials');
    expect(apiClient.post).toHaveBeenNthCalledWith(
      1,
      '/api/admin/applications/app-1/credentials/client-secrets',
      {
        description: 'Primary secret',
        expiresAt: '2026-06-24T12:00:00Z',
        revokeExisting: true,
      }
    );
    expect(apiClient.post).toHaveBeenNthCalledWith(
      2,
      '/api/admin/applications/app-1/credentials/certificates',
      {
        thumbprint: 'ABC123',
        subject: 'CN=worker.example.com',
        description: 'Worker certificate',
        expiresAt: '2027-05-24T12:00:00Z',
      }
    );
    expect(apiClient.delete).toHaveBeenCalledWith('/api/admin/applications/app-1/credentials/cred-1');
  });

  it('fetches application profile policies with availability metadata', async () => {
    const response = [{
      applicationProfile: ApplicationProfile.Web,
      isSelectable: true,
      unavailabilityReason: null,
      defaultClientProfile: ApplicationClientType.Confidential,
      allowedClientProfiles: [ApplicationClientType.Confidential],
      allowedGrantTypes: ['authorization_code', 'refresh_token'],
      defaultGrantTypes: ['authorization_code'],
      options: {
        clientSecrets: ApplicationOptionAvailability.Available,
        clientCertificates: ApplicationOptionAvailability.Advanced,
        pkce: ApplicationOptionAvailability.Available,
      },
      requirePkce: false,
      defaultRequirePkce: true,
      defaultRequireConsent: true,
      requiresRedirectUris: true,
    }];
    apiClient.get.mockResolvedValueOnce(response);

    await expect(getApplicationProfilePolicies()).resolves.toBe(response);

    expect(apiClient.get).toHaveBeenCalledWith('/api/admin/applications/policies/profiles');
  });
});
