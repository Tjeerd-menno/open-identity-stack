import type { AdminApiClient } from './index';

export type ProviderStatus = 'Active' | 'Disabled';

export type Provider = {
  id: string;
  name: string;
  displayName: string;
  authority: string;
  clientId: string;
  scopes: string[];
  jitProvisioningEnabled: boolean;
  trustEmailVerification?: boolean;
  status: ProviderStatus;
  createdAt: string;
};

export type CreateProviderRequest = {
  name: string;
  displayName?: string;
  authority: string;
  clientId: string;
  clientSecret?: string;
  scopes?: string[];
  jitProvisioningEnabled?: boolean;
};

export type UpdateProviderRequest = {
  displayName?: string;
  clientId?: string;
  clientSecret?: string;
  scopes?: string[];
  jitProvisioningEnabled?: boolean;
  status?: ProviderStatus;
};

export type ProvidersContract = {
  getProviders: (includeDisabled?: boolean) => Promise<Provider[]>;
  getProvider: (providerId: string) => Promise<Provider>;
  createProvider: (data: CreateProviderRequest) => Promise<Provider>;
  updateProvider: (providerId: string, data: UpdateProviderRequest) => Promise<Provider>;
  enableProvider: (providerId: string) => Promise<Provider>;
  disableProvider: (providerId: string) => Promise<Provider>;
  deleteProvider: (providerId: string) => Promise<void>;
  setEmailVerificationTrust: (providerId: string, trusted: boolean) => Promise<void>;
};

export function createProvidersContract(client: AdminApiClient): ProvidersContract {
  return {
    getProviders: (includeDisabled = false) =>
      client.get<Provider[]>('/api/admin/providers', { includeDisabled }),
    getProvider: (providerId) => client.get<Provider>(`/api/admin/providers/${providerId}`),
    createProvider: (data) => client.post<Provider>('/api/admin/providers', data),
    updateProvider: (providerId, data) => client.patch<Provider>(`/api/admin/providers/${providerId}`, data),
    enableProvider: (providerId) => client.post<Provider>(`/api/admin/providers/${providerId}/enable`),
    disableProvider: (providerId) => client.post<Provider>(`/api/admin/providers/${providerId}/disable`),
    deleteProvider: (providerId) => client.delete<void>(`/api/admin/providers/${providerId}`),
    setEmailVerificationTrust: (providerId, trusted) => client.put<void>(`/api/admin/providers/${providerId}/email-verification-trust`, { trusted }),
  };
}
