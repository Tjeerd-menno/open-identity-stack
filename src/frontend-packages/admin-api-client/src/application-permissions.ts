import type { AdminApiClient, PaginatedResponse, RequestParams } from './index';

export const basePath = '/api/admin/application-permissions';

export type PrincipalType = 'User' | 'Group';
export type OwnerType = 'user' | 'group' | PrincipalType;
export type ApplicationPermissionStatus = 'Active' | 'Disabled' | 'Removed';

export type RegisteredApplicationListItem = {
  id: string;
  applicationIdentifier: string;
  displayName: string;
  ownerId: string;
  ownerType: PrincipalType;
  permissionCount: number;
  status: ApplicationPermissionStatus;
  manifestVersion?: string | null;
  updatedAt?: string | null;
};

export type RegisteredApplicationPermission = {
  id: string;
  key?: string;
  fullPermissionKey: string;
  displayName?: string | null;
  description?: string | null;
  category?: string | null;
  status?: ApplicationPermissionStatus;
};

export type DelegatedMaintainer = {
  id: string;
  principalId: string;
  principalType: PrincipalType;
};

export type RegisteredApplication = RegisteredApplicationListItem & {
  description?: string | null;
  schemaVersion?: string | null;
  manifestBaseUrl?: string | null;
  concurrencyToken: number;
  permissions: RegisteredApplicationPermission[];
  maintainers: DelegatedMaintainer[];
};

export type PermissionManifestPermission = {
  key: string;
  displayName: string;
  description?: string | null;
  category?: string | null;
};

export type PermissionManifestRequest = {
  manifest: {
    schemaVersion: '1.0.0';
    application: {
      id: string;
      displayName: string;
      description?: string | null;
      version: string;
    };
    permissions: PermissionManifestPermission[];
  };
  ownerId: string;
  ownerType: OwnerType;
  manifestBaseUrl?: string | null;
};

export type AddApplicationPermissionRequest = {
  permissionKey: string;
  displayName: string;
  description?: string | null;
  category?: string | null;
  concurrencyToken?: number;
};

export type ChangeLifecycleRequest = {
  status: 'Active' | 'Disabled';
  acknowledgeDependencies: boolean;
  concurrencyToken?: number;
};

export type TransferApplicationOwnershipRequest = {
  ownerId: string;
  ownerType: PrincipalType;
  concurrencyToken?: number;
};

export type AddDelegatedMaintainerRequest = {
  principalId: string;
  principalType: PrincipalType;
  concurrencyToken?: number;
};

export type UpdateRegisteredApplicationRequest = {
  displayName: string;
  description?: string | null;
  manifestBaseUrl?: string | null;
  concurrencyToken?: number;
};

export type ManifestPreview = {
  applicationId?: string;
  removals?: unknown[];
};

export type ManifestApplyResult = {
  application: RegisteredApplication;
  result: unknown;
};

export type AssignablePermissionCatalogItem = {
  id?: string;
  fullPermissionKey: string;
  displayName?: string | null;
  applicationIdentifier?: string | null;
};

export type ApplicationPermissionHistory = {
  deletedApplications: unknown[];
  removedPermissions: Array<{
    id?: string;
    fullPermissionKey: string;
    removedAt?: string | null;
  }>;
};

export type PermissionDiagnostics = {
  issues: Array<{
    fullPermissionKey: string;
    roleName?: string | null;
    issueType?: string | null;
  }>;
};

export type ReplacementGuidanceRequest = {
  replacementFullPermissionKey?: string | null;
  replacementNote?: string | null;
  concurrencyToken?: number;
};

export type RemovedPermissionDetail = {
  id: string;
};

export type PaginationParams = {
  page?: number;
  pageSize?: number;
  search?: string;
};

export type RoleAssignmentDependency = Record<string, unknown>;

export type ApplicationPermissionsContract = {
  getRegisteredApplications: (
    params?: PaginationParams
  ) => Promise<PaginatedResponse<RegisteredApplicationListItem>>;
  getRegisteredApplication: (id: string) => Promise<RegisteredApplication>;
  registerPermissionManifest: (data: PermissionManifestRequest) => Promise<{ id: string }>;
  importPermissionManifestFromEndpoint: (endpoint: string) => Promise<{ id: string }>;
  previewPermissionManifest: (
    id: string,
    data: PermissionManifestRequest,
    concurrencyToken?: number
  ) => Promise<ManifestPreview>;
  applyPermissionManifest: (
    id: string,
    data: PermissionManifestRequest,
    concurrencyToken?: number
  ) => Promise<ManifestApplyResult>;
  previewRemotePermissionManifest: (id: string, concurrencyToken?: number) => Promise<ManifestPreview>;
  applyRemotePermissionManifest: (id: string, concurrencyToken?: number) => Promise<ManifestApplyResult>;
  updateRegisteredApplication: (id: string, data: UpdateRegisteredApplicationRequest) => Promise<RegisteredApplication>;
  addApplicationPermission: (id: string, data: AddApplicationPermissionRequest) => Promise<RegisteredApplication>;
  changeApplicationLifecycle: (id: string, data: ChangeLifecycleRequest) => Promise<RegisteredApplication>;
  transferApplicationOwnership: (id: string, data: TransferApplicationOwnershipRequest) => Promise<RegisteredApplication>;
  addDelegatedMaintainer: (id: string, data: AddDelegatedMaintainerRequest) => Promise<RegisteredApplication>;
  removeDelegatedMaintainer: (
    id: string,
    principalId: string,
    concurrencyToken?: number
  ) => Promise<RegisteredApplication>;
  getAssignablePermissionCatalog: (
    params?: PaginationParams
  ) => Promise<PaginatedResponse<AssignablePermissionCatalogItem>>;
  getApplicationPermissionHistory: (params?: { applicationIdentifier?: string }) => Promise<ApplicationPermissionHistory>;
  getApplicationPermissionDiagnostics: () => Promise<PermissionDiagnostics>;
  updateRemovedPermissionReplacement: (
    permissionId: string,
    data: ReplacementGuidanceRequest
  ) => Promise<RemovedPermissionDetail>;
  getPermissionDependencies: (permissionId: string) => Promise<RoleAssignmentDependency[]>;
};

export function createApplicationPermissionsContract(client: AdminApiClient): ApplicationPermissionsContract {
  return {
    getRegisteredApplications: (params) =>
      client.get<PaginatedResponse<RegisteredApplicationListItem>>(`${basePath}/applications`, params),
    getRegisteredApplication: (id) => client.get<RegisteredApplication>(`${basePath}/applications/${id}`),
    registerPermissionManifest: (data) =>
      normalizeApplicationPermissionMutationResponse(
        client.post<{ id?: string; applicationId?: string }>(`${basePath}/applications`, data)
      ),
    importPermissionManifestFromEndpoint: (endpoint) =>
      normalizeApplicationPermissionMutationResponse(
        client.post<{ id?: string; applicationId?: string }>(`${basePath}/applications/import`, { endpoint })
      ),
    previewPermissionManifest: (id, data, concurrencyToken) =>
      client.post<ManifestPreview>(`${basePath}/applications/${id}/manifest/preview`, {
        manifest: data.manifest,
        concurrencyToken,
      }),
    applyPermissionManifest: (id, data, concurrencyToken) =>
      client.post<ManifestApplyResult>(`${basePath}/applications/${id}/manifest`, {
        manifest: data.manifest,
        concurrencyToken,
      }),
    previewRemotePermissionManifest: (id, concurrencyToken) =>
      client.post<ManifestPreview>(`${basePath}/applications/${id}/import/preview`, {
        concurrencyToken,
      }),
    applyRemotePermissionManifest: (id, concurrencyToken) =>
      client.post<ManifestApplyResult>(`${basePath}/applications/${id}/import`, {
        concurrencyToken,
      }),
    updateRegisteredApplication: (id, data) =>
      client.patch<RegisteredApplication>(`${basePath}/applications/${id}`, data),
    addApplicationPermission: (id, data) =>
      client.post<RegisteredApplication>(`${basePath}/applications/${id}/permissions`, data),
    changeApplicationLifecycle: (id, data) =>
      client.post<RegisteredApplication>(`${basePath}/applications/${id}/lifecycle`, data),
    transferApplicationOwnership: (id, data) =>
      client.post<RegisteredApplication>(`${basePath}/applications/${id}/ownership`, data),
    addDelegatedMaintainer: (id, data) =>
      client.post<RegisteredApplication>(`${basePath}/applications/${id}/maintainers`, data),
    removeDelegatedMaintainer: (id, principalId, concurrencyToken) =>
      client.delete<RegisteredApplication>(
        `${basePath}/applications/${id}/maintainers/${encodeURIComponent(principalId)}${queryFromRequestParams({
          concurrencyToken,
        })}`
      ),
    getAssignablePermissionCatalog: (params) =>
      client.get<PaginatedResponse<AssignablePermissionCatalogItem>>(`${basePath}/catalog`, params),
    getApplicationPermissionHistory: (params) =>
      client.get<ApplicationPermissionHistory>(`${basePath}/history`, params),
    getApplicationPermissionDiagnostics: () =>
      client.get<PermissionDiagnostics>(`${basePath}/diagnostics`),
    updateRemovedPermissionReplacement: (permissionId, data) =>
      client.patch<RemovedPermissionDetail>(
        `${basePath}/permissions/${permissionId}/replacement`,
        data
      ),
    getPermissionDependencies: (permissionId) =>
      client.get<RoleAssignmentDependency[]>(`${basePath}/permissions/${permissionId}/dependencies`),
  };
}

function normalizeApplicationPermissionMutationResponse(
  promise: Promise<{ id?: string; applicationId?: string }>
): Promise<{ id: string }> {
  return promise.then((response) => ({
    id: response.id ?? response.applicationId ?? '',
  }));
}

function queryFromRequestParams(params: RequestParams): string {
  const query = Object.entries(params)
    .filter(([, value]) => value !== undefined)
    .map(([key, value]) => `${key}=${encodeURIComponent(String(value))}`)
    .join('&');

  return query.length > 0 ? `?${query}` : '';
}
