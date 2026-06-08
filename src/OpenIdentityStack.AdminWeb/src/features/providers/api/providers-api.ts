import { adminApiClient } from '@/lib/api/client';
import type { ProviderListResponse } from '@/types';
import {
  createProvidersContract,
  type CreateProviderRequest,
  type Provider,
  type UpdateProviderRequest,
} from '@openidentitystack/admin-api-client';

const contract = createProvidersContract(adminApiClient);

export const providersApi = {
  /**
   * Get list of providers
   * Note: API returns array directly, not paginated response
   */
  async getProviders(includeDisabled = false): Promise<ProviderListResponse> {
    return contract.getProviders(includeDisabled) as Promise<ProviderListResponse>;
  },

  /**
   * Get single provider by ID
   */
  async getProvider(id: string): Promise<Provider> {
    return contract.getProvider(id) as Promise<Provider>;
  },

  /**
   * Create a new OIDC provider
   */
  async createProvider(data: CreateProviderRequest): Promise<Provider> {
    return contract.createProvider(data) as Promise<Provider>;
  },

  /**
   * Update an existing provider
   */
  async updateProvider(id: string, data: UpdateProviderRequest): Promise<Provider> {
    return contract.updateProvider(id, data) as Promise<Provider>;
  },

  /**
   * Delete a provider
   */
  async deleteProvider(id: string): Promise<void> {
    await contract.deleteProvider(id);
  },

  /**
   * Enable a provider
   */
  async enableProvider(id: string): Promise<Provider> {
    return contract.enableProvider(id) as Promise<Provider>;
  },

  /**
   * Disable a provider
   */
  async disableProvider(id: string): Promise<Provider> {
    return contract.disableProvider(id) as Promise<Provider>;
  }
};
