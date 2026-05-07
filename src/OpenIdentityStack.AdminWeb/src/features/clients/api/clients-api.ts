/**
 * Clients API Client
 * 
 * Provides functions for interacting with the client management endpoints
 */

import { apiClient } from '@/lib/api/client';
import type {
  Client,
  ClientListResponse,
  ClientCreatedResponse,
  CreateClientRequest,
  UpdateClientRequest,
  PaginationParams,
} from '@/types';

/**
 * Get paginated list of clients
 */
export async function getClients(
  params?: PaginationParams
): Promise<ClientListResponse> {
  return await apiClient.get<ClientListResponse>(
    '/api/admin/clients',
    params
  );
}

/**
 * Get a single client by ID
 */
export async function getClient(
  clientId: string
): Promise<Client> {
  return await apiClient.get<Client>(
    `/api/admin/clients/${clientId}`
  );
}

/**
 * Create a new client
 * Note: The response includes clientSecret for confidential clients which is only returned once
 */
export async function createClient(
  data: CreateClientRequest
): Promise<ClientCreatedResponse> {
  return await apiClient.post<ClientCreatedResponse>(
    '/api/admin/clients',
    data
  );
}

/**
 * Update an existing client
 */
export async function updateClient(
  clientId: string,
  data: UpdateClientRequest
): Promise<Client> {
  return await apiClient.patch<Client>(
    `/api/admin/clients/${clientId}`,
    data
  );
}

/**
 * Delete a client
 */
export async function deleteClient(
  clientId: string
): Promise<void> {
  return await apiClient.delete<void>(
    `/api/admin/clients/${clientId}`
  );
}
