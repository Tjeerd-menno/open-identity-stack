import { adminApiClient } from '@/lib/api/client';
import {
  createSettingsContract,
  type AuthenticationSettings,
  type AuthenticationProvider,
  type SetDefaultProviderRequest,
  type SetLocalFallbackRequest,
} from '@openidentitystack/admin-api-client';

const contract = createSettingsContract(adminApiClient);

export const settingsApi = {
  /**
   * Get current authentication settings
   */
  async getAuthenticationSettings(): Promise<AuthenticationSettings> {
    return contract.getAuthenticationSettings();
  },

  /**
   * List available authentication providers
   */
  async getAuthenticationProviders(): Promise<AuthenticationProvider[]> {
    return contract.getAuthenticationProviders();
  },

  /**
   * Set the default authentication provider
   */
  async setDefaultProvider(data: SetDefaultProviderRequest): Promise<AuthenticationSettings> {
    return contract.setDefaultProvider(data);
  },

  /**
   * Enable or disable local fallback authentication for IAM admins
   */
  async setLocalFallback(data: SetLocalFallbackRequest): Promise<AuthenticationSettings> {
    return contract.setLocalFallback(data);
  },
};
