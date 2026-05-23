import { apiClient } from '@/lib/api/client';
import type {
  AddDelegatedMaintainerRequest,
  AddApplicationPermissionRequest,
  AssignablePermissionCatalogResponse,
  ChangeLifecycleRequest,
  ImportPermissionManifestRequest,
  PaginationParams,
  PermissionManifestRequest,
  RegisteredApplication,
  RegisteredApplicationListResponse,
  RoleAssignmentDependency,
  TransferApplicationOwnershipRequest,
  UpdateRegisteredApplicationRequest,
} from '@/types';

const BASE_PATH = '/api/admin/application-permissions';

export async function getRegisteredApplications(params: PaginationParams = {}): Promise<RegisteredApplicationListResponse> {
  return await apiClient.get<RegisteredApplicationListResponse>(`${BASE_PATH}/applications`, params);
}

export async function getRegisteredApplication(id: string): Promise<RegisteredApplication> {
  return await apiClient.get<RegisteredApplication>(`${BASE_PATH}/applications/${id}`);
}

export async function registerPermissionManifest(data: PermissionManifestRequest): Promise<{ id: string }> {
  const response = await apiClient.post<{ applicationId: string; applicationIdentifier: string; permissionsRegistered: number }>(`${BASE_PATH}/applications`, data);
  return { id: response.applicationId };
}

export async function importPermissionManifestFromEndpoint(endpoint: string): Promise<{ id: string }> {
  const request: ImportPermissionManifestRequest = { endpoint };
  const response = await apiClient.post<{ applicationId: string; applicationIdentifier: string; permissionsRegistered: number }>(`${BASE_PATH}/applications/import`, request);
  return { id: response.applicationId };
}

export async function updateRegisteredApplication(id: string, data: UpdateRegisteredApplicationRequest): Promise<RegisteredApplication> {
  return await apiClient.patch<RegisteredApplication>(`${BASE_PATH}/applications/${id}`, data);
}

export async function addApplicationPermission(id: string, data: AddApplicationPermissionRequest): Promise<RegisteredApplication> {
  return await apiClient.post<RegisteredApplication>(`${BASE_PATH}/applications/${id}/permissions`, data);
}

export async function changeApplicationLifecycle(id: string, data: ChangeLifecycleRequest): Promise<RegisteredApplication> {
  return await apiClient.post<RegisteredApplication>(`${BASE_PATH}/applications/${id}/lifecycle`, data);
}

export async function changePermissionLifecycle(permissionId: string, data: ChangeLifecycleRequest): Promise<RegisteredApplication> {
  return await apiClient.post<RegisteredApplication>(`${BASE_PATH}/permissions/${permissionId}/lifecycle`, data);
}

export async function getPermissionDependencies(permissionId: string): Promise<RoleAssignmentDependency[]> {
  return await apiClient.get<RoleAssignmentDependency[]>(`${BASE_PATH}/permissions/${permissionId}/dependencies`);
}

export async function transferApplicationOwnership(id: string, data: TransferApplicationOwnershipRequest): Promise<RegisteredApplication> {
  return await apiClient.post<RegisteredApplication>(`${BASE_PATH}/applications/${id}/ownership`, data);
}

export async function addDelegatedMaintainer(id: string, data: AddDelegatedMaintainerRequest): Promise<RegisteredApplication> {
  return await apiClient.post<RegisteredApplication>(`${BASE_PATH}/applications/${id}/maintainers`, data);
}

export async function removeDelegatedMaintainer(id: string, principalId: string, concurrencyToken?: number): Promise<RegisteredApplication> {
  return await apiClient.delete<RegisteredApplication>(
    `${BASE_PATH}/applications/${id}/maintainers/${encodeURIComponent(principalId)}${concurrencyToken === undefined ? '' : `?concurrencyToken=${concurrencyToken}`}`
  );
}

export async function getAssignablePermissionCatalog(params: PaginationParams = {}): Promise<AssignablePermissionCatalogResponse> {
  return await apiClient.get<AssignablePermissionCatalogResponse>(`${BASE_PATH}/catalog`, params);
}
