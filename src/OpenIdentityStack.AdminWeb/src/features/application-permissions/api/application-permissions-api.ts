import { adminApiClient } from '@/lib/api/client';
import type { PaginationParams } from '@openidentitystack/admin-api-client';
import {
  createApplicationPermissionsContract,
  type AddDelegatedMaintainerRequest,
  type AddApplicationPermissionRequest,
  type AssignablePermissionCatalogItem,
  type ApplicationPermissionHistory,
  type ChangeLifecycleRequest,
  type ManifestApplyResult,
  type ManifestPreview,
  type PermissionDiagnostics,
  type PermissionManifestRequest,
  type RegisteredApplication,
  type RegisteredApplicationListItem,
  type RemovedPermissionDetail,
  type ReplacementGuidanceRequest,
  type RoleAssignmentDependency,
  type TransferApplicationOwnershipRequest,
  type UpdateRegisteredApplicationRequest,
} from '@openidentitystack/admin-api-client';

const contract = createApplicationPermissionsContract(adminApiClient);

export async function getRegisteredApplications(params: PaginationParams = {}): Promise<{
  items: RegisteredApplicationListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}> {
  return contract.getRegisteredApplications(params);
}

export async function getRegisteredApplication(id: string): Promise<RegisteredApplication> {
  return contract.getRegisteredApplication(id);
}

export async function registerPermissionManifest(data: PermissionManifestRequest): Promise<{ id: string }> {
  return contract.registerPermissionManifest(data);
}

export async function previewPermissionManifest(
  id: string,
  data: PermissionManifestRequest,
  concurrencyToken?: number
): Promise<ManifestPreview> {
  return contract.previewPermissionManifest(id, data, concurrencyToken);
}

export async function applyPermissionManifest(
  id: string,
  data: PermissionManifestRequest,
  concurrencyToken?: number
): Promise<ManifestApplyResult> {
  return contract.applyPermissionManifest(id, data, concurrencyToken);
}

export async function previewRemotePermissionManifest(
  id: string,
  concurrencyToken?: number
): Promise<ManifestPreview> {
  return contract.previewRemotePermissionManifest(id, concurrencyToken);
}

export async function applyRemotePermissionManifest(
  id: string,
  concurrencyToken?: number
): Promise<ManifestApplyResult> {
  return contract.applyRemotePermissionManifest(id, concurrencyToken);
}

export async function importPermissionManifestFromEndpoint(endpoint: string): Promise<{ id: string }> {
  return contract.importPermissionManifestFromEndpoint(endpoint);
}

export async function updateRegisteredApplication(
  id: string,
  data: UpdateRegisteredApplicationRequest
): Promise<RegisteredApplication> {
  return contract.updateRegisteredApplication(id, data);
}

export async function addApplicationPermission(
  id: string,
  data: AddApplicationPermissionRequest
): Promise<RegisteredApplication> {
  return contract.addApplicationPermission(id, data);
}

export async function changeApplicationLifecycle(
  id: string,
  data: ChangeLifecycleRequest
): Promise<RegisteredApplication> {
  return contract.changeApplicationLifecycle(id, data);
}

export async function getPermissionDependencies(permissionId: string): Promise<RoleAssignmentDependency[]> {
  return contract.getPermissionDependencies(permissionId);
}

export async function transferApplicationOwnership(
  id: string,
  data: TransferApplicationOwnershipRequest
): Promise<RegisteredApplication> {
  return contract.transferApplicationOwnership(id, data);
}

export async function addDelegatedMaintainer(
  id: string,
  data: AddDelegatedMaintainerRequest
): Promise<RegisteredApplication> {
  return contract.addDelegatedMaintainer(id, data);
}

export async function removeDelegatedMaintainer(
  id: string,
  principalId: string,
  concurrencyToken?: number
): Promise<RegisteredApplication> {
  return contract.removeDelegatedMaintainer(id, principalId, concurrencyToken);
}

export async function getAssignablePermissionCatalog(
  params: PaginationParams = {}
): Promise<{
  items: AssignablePermissionCatalogItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}> {
  return contract.getAssignablePermissionCatalog(params);
}

export async function getApplicationPermissionHistory(
  params: { applicationIdentifier?: string } = {}
): Promise<ApplicationPermissionHistory> {
  return contract.getApplicationPermissionHistory(params);
}

export async function getApplicationPermissionDiagnostics(): Promise<PermissionDiagnostics> {
  return contract.getApplicationPermissionDiagnostics();
}

export async function updateRemovedPermissionReplacement(
  permissionId: string,
  data: ReplacementGuidanceRequest
): Promise<RemovedPermissionDetail> {
  return contract.updateRemovedPermissionReplacement(permissionId, data);
}

export type {
  AddDelegatedMaintainerRequest,
  AddApplicationPermissionRequest,
  AssignablePermissionCatalogItem,
  ApplicationPermissionHistory,
  ChangeLifecycleRequest,
  ManifestApplyResult,
  ManifestPreview,
  PermissionDiagnostics,
  PermissionManifestRequest,
  RegisteredApplication,
  RegisteredApplicationListItem,
  RemovedPermissionDetail,
  ReplacementGuidanceRequest,
  RoleAssignmentDependency,
  TransferApplicationOwnershipRequest,
  UpdateRegisteredApplicationRequest,
};
