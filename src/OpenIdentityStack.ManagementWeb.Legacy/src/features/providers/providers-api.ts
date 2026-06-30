import { adminApiClient } from '@/lib/admin-api';
import {
  createProvidersContract,
  type CreateProviderRequest,
  type ProviderStatus,
  type Provider as SharedProvider,
  type UpdateProviderRequest,
} from '@openidentitystack/admin-api-client';

const contract = createProvidersContract(adminApiClient);

export type Provider = SharedProvider;
export type { CreateProviderRequest, ProviderStatus, UpdateProviderRequest };

export function getProviders(includeDisabled = false): Promise<Provider[]> {
  return contract.getProviders(includeDisabled);
}

export function getProvider(providerId: string): Promise<Provider> {
  return contract.getProvider(providerId);
}

export function createProvider(data: CreateProviderRequest): Promise<Provider> {
  return contract.createProvider(data);
}

export function updateProvider(providerId: string, data: UpdateProviderRequest): Promise<Provider> {
  return contract.updateProvider(providerId, data);
}

export function enableProvider(providerId: string): Promise<Provider> {
  return contract.enableProvider(providerId);
}

export function disableProvider(providerId: string): Promise<Provider> {
  return contract.disableProvider(providerId);
}

export function deleteProvider(providerId: string): Promise<void> {
  return contract.deleteProvider(providerId);
}
