import type { AdminApiClient } from './index';

export type AuthenticationSettings = {
  defaultProviderId: string;
  isLocalDefault: boolean;
  localFallbackEnabled: boolean;
  updatedAt: string;
};

export type AuthenticationProvider = {
  id: string;
  name: string;
  displayName: string;
  type: 'local' | 'external';
  isActive: boolean;
};

export type SetDefaultProviderRequest = {
  providerId: string;
};

export type SetLocalFallbackRequest = {
  enabled: boolean;
};

export type SettingsContract = {
  getAuthenticationSettings: () => Promise<AuthenticationSettings>;
  getAuthenticationProviders: () => Promise<AuthenticationProvider[]>;
  setDefaultProvider: (data: SetDefaultProviderRequest) => Promise<AuthenticationSettings>;
  setLocalFallback: (data: SetLocalFallbackRequest) => Promise<AuthenticationSettings>;
};

export function createSettingsContract(client: AdminApiClient): SettingsContract {
  return {
    getAuthenticationSettings: () => client.get<AuthenticationSettings>('/api/admin/authentication-settings'),
    getAuthenticationProviders: () => client.get<AuthenticationProvider[]>('/api/admin/authentication-settings/providers'),
    setDefaultProvider: (data) => client.put<AuthenticationSettings>('/api/admin/authentication-settings/default-provider', data),
    setLocalFallback: (data) => client.put<AuthenticationSettings>('/api/admin/authentication-settings/local-fallback', data),
  };
}
