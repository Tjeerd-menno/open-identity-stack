import { request } from '@/lib/admin-api';

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

export function getAuthenticationSettings(): Promise<AuthenticationSettings> {
  return request<AuthenticationSettings>('/api/admin/authentication-settings');
}

export function getAuthenticationProviders(): Promise<AuthenticationProvider[]> {
  return request<AuthenticationProvider[]>('/api/admin/authentication-settings/providers');
}

export function setDefaultProvider(data: SetDefaultProviderRequest): Promise<AuthenticationSettings> {
  return request<AuthenticationSettings>('/api/admin/authentication-settings/default-provider', {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export function setLocalFallback(data: SetLocalFallbackRequest): Promise<AuthenticationSettings> {
  return request<AuthenticationSettings>('/api/admin/authentication-settings/local-fallback', {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}
