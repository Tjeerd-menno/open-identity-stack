import { adminApiClient } from '@/lib/admin-api';
import { createApplicationPermissionsContract } from '@openidentitystack/admin-api-client';
import type {
  AddApplicationPermissionRequest,
  AddDelegatedMaintainerRequest,
  AddDelegatedMaintainerRequest as AddDelegatedMaintainerPayload,
  ApplicationPermissionHistory,
  ApplicationPermissionStatus,
  ChangeLifecycleRequest,
  ManifestApplyResult,
  ManifestPreview,
  PaginationParams,
  PermissionDiagnostics,
  PermissionManifestPermission,
  PermissionManifestRequest,
  RegisteredApplication,
  RegisteredApplicationListItem,
  RegisteredApplicationPermission,
  RemovedPermissionDetail,
  ReplacementGuidanceRequest,
  TransferApplicationOwnershipRequest,
  UpdateRegisteredApplicationRequest,
  PrincipalType,
  OwnerType,
  RoleAssignmentDependency,
  AssignablePermissionCatalogItem,
  PaginatedResponse,
} from '@openidentitystack/admin-api-client';

const contract = createApplicationPermissionsContract(adminApiClient);

export type { AddApplicationPermissionRequest, AddDelegatedMaintainerPayload as AddDelegatedMaintainerRequest };
export type { ApplicationPermissionHistory, ChangeLifecycleRequest, ManifestApplyResult };
export type {
  ApplicationPermissionStatus,
  ManifestPreview,
  PaginationParams,
  PermissionDiagnostics,
  PermissionManifestPermission,
  PermissionManifestRequest,
  RegisteredApplication,
  RegisteredApplicationListItem,
  RegisteredApplicationPermission,
  RemovedPermissionDetail,
  ReplacementGuidanceRequest,
  TransferApplicationOwnershipRequest,
  UpdateRegisteredApplicationRequest,
  PrincipalType,
  OwnerType,
  RoleAssignmentDependency,
  AssignablePermissionCatalogItem,
  PaginatedResponse,
};

export function getRegisteredApplications(params?: PaginationParams) {
  return contract.getRegisteredApplications(params);
}

export function getRegisteredApplication(id: string): Promise<RegisteredApplication> {
  return contract.getRegisteredApplication(id);
}

export function registerPermissionManifest(data: PermissionManifestRequest): Promise<{ id: string }> {
  return contract.registerPermissionManifest(data);
}

export function importPermissionManifestFromEndpoint(endpoint: string): Promise<{ id: string }> {
  return contract.importPermissionManifestFromEndpoint(endpoint);
}

export function previewPermissionManifest(id: string, data: PermissionManifestRequest, concurrencyToken?: number): Promise<ManifestPreview> {
  return contract.previewPermissionManifest(id, data, concurrencyToken);
}

export function applyPermissionManifest(id: string, data: PermissionManifestRequest, concurrencyToken?: number): Promise<ManifestApplyResult> {
  return contract.applyPermissionManifest(id, data, concurrencyToken);
}

export function previewRemotePermissionManifest(id: string, concurrencyToken?: number): Promise<ManifestPreview> {
  return contract.previewRemotePermissionManifest(id, concurrencyToken);
}

export function applyRemotePermissionManifest(id: string, concurrencyToken?: number): Promise<ManifestApplyResult> {
  return contract.applyRemotePermissionManifest(id, concurrencyToken);
}

export function updateRegisteredApplication(id: string, data: UpdateRegisteredApplicationRequest): Promise<RegisteredApplication> {
  return contract.updateRegisteredApplication(id, data);
}

export function addApplicationPermission(id: string, data: AddApplicationPermissionRequest): Promise<RegisteredApplication> {
  return contract.addApplicationPermission(id, data);
}

export function changeApplicationLifecycle(id: string, data: ChangeLifecycleRequest): Promise<RegisteredApplication> {
  return contract.changeApplicationLifecycle(id, data);
}

export function transferApplicationOwnership(id: string, data: TransferApplicationOwnershipRequest): Promise<RegisteredApplication> {
  return contract.transferApplicationOwnership(id, data);
}

export function addDelegatedMaintainer(id: string, data: AddDelegatedMaintainerRequest): Promise<RegisteredApplication> {
  return contract.addDelegatedMaintainer(id, data as AddDelegatedMaintainerPayload);
}

export function removeDelegatedMaintainer(
  id: string,
  principalId: string,
  concurrencyToken?: number
): Promise<RegisteredApplication> {
  return contract.removeDelegatedMaintainer(id, principalId, concurrencyToken);
}

export function getAssignablePermissionCatalog(params?: PaginationParams): Promise<PaginatedResponse<AssignablePermissionCatalogItem>> {
  return contract.getAssignablePermissionCatalog(params);
}

export function getApplicationPermissionHistory(params?: { applicationIdentifier?: string }): Promise<ApplicationPermissionHistory> {
  return contract.getApplicationPermissionHistory(params);
}

export function getApplicationPermissionDiagnostics(): Promise<PermissionDiagnostics> {
  return contract.getApplicationPermissionDiagnostics();
}

export function updateRemovedPermissionReplacement(
  permissionId: string,
  data: ReplacementGuidanceRequest
): Promise<RemovedPermissionDetail> {
  return contract.updateRemovedPermissionReplacement(permissionId, data);
}

export function getPermissionDependencies(permissionId: string): Promise<RoleAssignmentDependency[]> {
  return contract.getPermissionDependencies(permissionId);
}
