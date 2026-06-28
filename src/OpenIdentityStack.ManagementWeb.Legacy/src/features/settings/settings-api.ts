import { adminApiClient } from '@/lib/admin-api';
import {
  createSettingsContract,
  type AuthenticationProvider,
  type AuthenticationSettings,
  type SetDefaultProviderRequest,
  type SetLocalFallbackRequest,
} from '@openidentitystack/admin-api-client';

const contract = createSettingsContract(adminApiClient);

export type { AuthenticationProvider, AuthenticationSettings, SetDefaultProviderRequest, SetLocalFallbackRequest };

export function getAuthenticationSettings(): Promise<AuthenticationSettings> {
  return contract.getAuthenticationSettings();
}

export function getAuthenticationProviders(): Promise<AuthenticationProvider[]> {
  return contract.getAuthenticationProviders();
}

export function setDefaultProvider(data: SetDefaultProviderRequest): Promise<AuthenticationSettings> {
  return contract.setDefaultProvider(data);
}

export function setLocalFallback(data: SetLocalFallbackRequest): Promise<AuthenticationSettings> {
  return contract.setLocalFallback(data);
}
