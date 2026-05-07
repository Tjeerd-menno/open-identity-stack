/**
 * Service Accounts API Client
 * 
 * Provides functions for interacting with the service accounts management endpoints
 */

import { apiClient } from '@/lib/api/client';
import type {
  ServiceAccount,
  ServiceAccountListResponse,
  ServiceAccountCreatedResponse,
  CreateServiceAccountRequest,
  UpdateServiceAccountRequest,
  RotateSecretResponse,
  RotateSecretRequest,
  AddCertificateRequest,
  AddCertificateResponse,
  Certificate,
  PaginationParams,
} from '@/types';

/**
 * Get paginated list of service accounts
 */
export async function getServiceAccounts(
  params?: PaginationParams
): Promise<ServiceAccountListResponse> {
  return await apiClient.get<ServiceAccountListResponse>(
    '/api/admin/service-accounts',
    params
  );
}

/**
 * Get a single service account by ID
 */
export async function getServiceAccount(
  serviceAccountId: string
): Promise<ServiceAccount> {
  return await apiClient.get<ServiceAccount>(
    `/api/admin/service-accounts/${serviceAccountId}`
  );
}

/**
 * Create a new service account
 * Note: The response includes initialSecret which is only returned once
 */
export async function createServiceAccount(
  data: CreateServiceAccountRequest
): Promise<ServiceAccountCreatedResponse> {
  return await apiClient.post<ServiceAccountCreatedResponse>(
    '/api/admin/service-accounts',
    data
  );
}

/**
 * Update an existing service account
 */
export async function updateServiceAccount(
  serviceAccountId: string,
  data: UpdateServiceAccountRequest
): Promise<ServiceAccount> {
  return await apiClient.patch<ServiceAccount>(
    `/api/admin/service-accounts/${serviceAccountId}`,
    data
  );
}

/**
 * Enable a service account
 */
export async function enableServiceAccount(
  serviceAccountId: string
): Promise<void> {
  return await apiClient.post<void>(
    `/api/admin/service-accounts/${serviceAccountId}/enable`
  );
}

/**
 * Disable a service account
 */
export async function disableServiceAccount(
  serviceAccountId: string
): Promise<void> {
  return await apiClient.post<void>(
    `/api/admin/service-accounts/${serviceAccountId}/disable`
  );
}

/**
 * Delete a service account
 */
export async function deleteServiceAccount(
  serviceAccountId: string
): Promise<void> {
  return await apiClient.delete<void>(
    `/api/admin/service-accounts/${serviceAccountId}`
  );
}

/**
 * Rotate the secret for a service account
 * Note: The response includes newSecret which is only returned once
 */
export async function rotateSecret(
  serviceAccountId: string,
  data: RotateSecretRequest
): Promise<RotateSecretResponse> {
  return await apiClient.post<RotateSecretResponse>(
    `/api/admin/service-accounts/${serviceAccountId}/rotate-secret`,
    data
  );
}

/**
 * Add a certificate to a service account
 */
export async function addCertificate(
  serviceAccountId: string,
  data: AddCertificateRequest
): Promise<AddCertificateResponse> {
  return await apiClient.post<AddCertificateResponse>(
    `/api/admin/service-accounts/${serviceAccountId}/certificates`,
    data
  );
}

/**
 * Get certificates for a service account
 */
export async function getServiceAccountCertificates(
  serviceAccountId: string
): Promise<Certificate[]> {
  return await apiClient.get<Certificate[]>(
    `/api/admin/service-accounts/${serviceAccountId}/certificates`
  );
}
