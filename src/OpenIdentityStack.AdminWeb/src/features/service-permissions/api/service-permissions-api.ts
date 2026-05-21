import { apiClient } from '@/lib/api/client';
import type {
  AddDelegatedMaintainerRequest,
  AddServicePermissionRequest,
  AssignablePermissionCatalogResponse,
  ChangeLifecycleRequest,
  PaginationParams,
  RegisteredService,
  RegisteredServiceListResponse,
  RegisterServiceRequest,
  RoleAssignmentDependency,
  TransferServiceOwnershipRequest,
  UpdateRegisteredServiceRequest,
} from '@/types';

const BASE_PATH = '/api/admin/service-permissions';

export async function getRegisteredServices(params: PaginationParams = {}): Promise<RegisteredServiceListResponse> {
  return await apiClient.get<RegisteredServiceListResponse>(`${BASE_PATH}/services`, params);
}

export async function getRegisteredService(id: string): Promise<RegisteredService> {
  return await apiClient.get<RegisteredService>(`${BASE_PATH}/services/${id}`);
}

export async function registerService(data: RegisterServiceRequest): Promise<{ id: string }> {
  const response = await apiClient.post<{ serviceId: string; serviceIdentifier: string; permissionsRegistered: number }>(`${BASE_PATH}/services`, data);
  return { id: response.serviceId };
}

export async function updateRegisteredService(id: string, data: UpdateRegisteredServiceRequest): Promise<RegisteredService> {
  return await apiClient.patch<RegisteredService>(`${BASE_PATH}/services/${id}`, data);
}

export async function addServicePermission(id: string, data: AddServicePermissionRequest): Promise<RegisteredService> {
  return await apiClient.post<RegisteredService>(`${BASE_PATH}/services/${id}/permissions`, data);
}

export async function changeServiceLifecycle(id: string, data: ChangeLifecycleRequest): Promise<RegisteredService> {
  return await apiClient.post<RegisteredService>(`${BASE_PATH}/services/${id}/lifecycle`, data);
}

export async function changePermissionLifecycle(permissionId: string, data: ChangeLifecycleRequest): Promise<RegisteredService> {
  return await apiClient.post<RegisteredService>(`${BASE_PATH}/permissions/${permissionId}/lifecycle`, data);
}

export async function getPermissionDependencies(permissionId: string): Promise<RoleAssignmentDependency[]> {
  return await apiClient.get<RoleAssignmentDependency[]>(`${BASE_PATH}/permissions/${permissionId}/dependencies`);
}

export async function transferServiceOwnership(id: string, data: TransferServiceOwnershipRequest): Promise<RegisteredService> {
  return await apiClient.post<RegisteredService>(`${BASE_PATH}/services/${id}/ownership`, data);
}

export async function addDelegatedMaintainer(id: string, data: AddDelegatedMaintainerRequest): Promise<RegisteredService> {
  return await apiClient.post<RegisteredService>(`${BASE_PATH}/services/${id}/maintainers`, data);
}

export async function removeDelegatedMaintainer(id: string, principalId: string, concurrencyToken?: number): Promise<RegisteredService> {
  return await apiClient.delete<RegisteredService>(
    `${BASE_PATH}/services/${id}/maintainers/${encodeURIComponent(principalId)}${concurrencyToken === undefined ? '' : `?concurrencyToken=${concurrencyToken}`}`
  );
}

export async function getAssignablePermissionCatalog(params: PaginationParams = {}): Promise<AssignablePermissionCatalogResponse> {
  return await apiClient.get<AssignablePermissionCatalogResponse>(`${BASE_PATH}/catalog`, params);
}
