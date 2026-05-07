import { apiClient } from '@/lib/api/client';
import type {
  AuthenticationSettings,
  AuthenticationProvider,
  SetDefaultProviderRequest,
  SetLocalFallbackRequest
} from '@/types';

const BASE_URL = '/api/admin/authentication-settings';

export const settingsApi = {
  /**
   * Get current authentication settings
   */
  async getAuthenticationSettings(): Promise<AuthenticationSettings> {
    const response = await apiClient.get<AuthenticationSettings>(BASE_URL);
    return response;
  },

  /**
   * List available authentication providers
   */
  async getAuthenticationProviders(): Promise<AuthenticationProvider[]> {
    const response = await apiClient.get<AuthenticationProvider[]>(`${BASE_URL}/providers`);
    return response;
  },

  /**
   * Set the default authentication provider
   */
  async setDefaultProvider(data: SetDefaultProviderRequest): Promise<AuthenticationSettings> {
    const response = await apiClient.put<AuthenticationSettings>(`${BASE_URL}/default-provider`, data);
    return response;
  },

  /**
   * Enable or disable local fallback authentication for IAM admins
   */
  async setLocalFallback(data: SetLocalFallbackRequest): Promise<AuthenticationSettings> {
    const response = await apiClient.put<AuthenticationSettings>(`${BASE_URL}/local-fallback`, data);
    return response;
  }
};
