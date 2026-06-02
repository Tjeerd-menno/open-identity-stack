import { request } from '@/lib/admin-api';

export type ProviderStatus = 'Active' | 'Disabled';

export type Provider = {
  id: string;
  name: string;
  displayName: string;
  authority: string;
  clientId: string;
  scopes: string[];
  jitProvisioningEnabled: boolean;
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

export function getProviders(includeDisabled = false): Promise<Provider[]> {
  return request<Provider[]>('/api/admin/providers', {}, { includeDisabled });
}

export function getProvider(providerId: string): Promise<Provider> {
  return request<Provider>(`/api/admin/providers/${providerId}`);
}

export function createProvider(data: CreateProviderRequest): Promise<Provider> {
  return request<Provider>('/api/admin/providers', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updateProvider(providerId: string, data: UpdateProviderRequest): Promise<Provider> {
  return request<Provider>(`/api/admin/providers/${providerId}`, {
    method: 'PATCH',
    body: JSON.stringify(data),
  });
}

export function enableProvider(providerId: string): Promise<Provider> {
  return request<Provider>(`/api/admin/providers/${providerId}/enable`, {
    method: 'POST',
  });
}

export function disableProvider(providerId: string): Promise<Provider> {
  return request<Provider>(`/api/admin/providers/${providerId}/disable`, {
    method: 'POST',
  });
}

export function deleteProvider(providerId: string): Promise<void> {
  return request<void>(`/api/admin/providers/${providerId}`, {
    method: 'DELETE',
  });
}
