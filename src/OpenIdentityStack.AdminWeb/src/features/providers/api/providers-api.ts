import { apiClient } from '@/lib/api/client';
import type {
  Provider,
  ProviderListResponse,
  CreateProviderRequest,
  UpdateProviderRequest
} from '@/types';

const BASE_URL = '/api/admin/providers';

export const providersApi = {
  /**
   * Get list of providers
   * Note: API returns array directly, not paginated response
   */
  async getProviders(includeDisabled = false): Promise<ProviderListResponse> {
    const response = await apiClient.get<ProviderListResponse>(BASE_URL, {
      includeDisabled
    });
    return response;
  },

  /**
   * Get single provider by ID
   */
  async getProvider(id: string): Promise<Provider> {
    const response = await apiClient.get<Provider>(`${BASE_URL}/${id}`);
    return response;
  },

  /**
   * Create a new OIDC provider
   */
  async createProvider(data: CreateProviderRequest): Promise<Provider> {
    const response = await apiClient.post<Provider>(BASE_URL, data);
    return response;
  },

  /**
   * Update an existing provider
   */
  async updateProvider(id: string, data: UpdateProviderRequest): Promise<Provider> {
    const response = await apiClient.patch<Provider>(`${BASE_URL}/${id}`, data);
    return response;
  },

  /**
   * Delete a provider
   */
  async deleteProvider(id: string): Promise<void> {
    await apiClient.delete(`${BASE_URL}/${id}`);
  },

  /**
   * Enable a provider
   */
  async enableProvider(id: string): Promise<Provider> {
    const response = await apiClient.post<Provider>(`${BASE_URL}/${id}/enable`);
    return response;
  },

  /**
   * Disable a provider
   */
  async disableProvider(id: string): Promise<Provider> {
    const response = await apiClient.post<Provider>(`${BASE_URL}/${id}/disable`);
    return response;
  }
};
