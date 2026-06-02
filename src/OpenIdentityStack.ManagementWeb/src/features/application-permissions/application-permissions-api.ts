import { request, type PaginatedResponse } from '@/lib/admin-api';

const basePath = '/api/admin/application-permissions';

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

export function getRegisteredApplications(params?: PaginationParams): Promise<PaginatedResponse<RegisteredApplicationListItem>> {
  return request<PaginatedResponse<RegisteredApplicationListItem>>(`${basePath}/applications`, {}, params);
}

export function getRegisteredApplication(id: string): Promise<RegisteredApplication> {
  return request<RegisteredApplication>(`${basePath}/applications/${id}`);
}

export async function registerPermissionManifest(data: PermissionManifestRequest): Promise<{ id: string }> {
  const response = await request<{ id?: string; applicationId?: string }>(`${basePath}/applications`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
  return { id: response.id ?? response.applicationId ?? '' };
}

export async function importPermissionManifestFromEndpoint(endpoint: string): Promise<{ id: string }> {
  const response = await request<{ id?: string; applicationId?: string }>(`${basePath}/applications/import`, {
    method: 'POST',
    body: JSON.stringify({ endpoint }),
  });
  return { id: response.id ?? response.applicationId ?? '' };
}

export function previewPermissionManifest(id: string, data: PermissionManifestRequest, concurrencyToken?: number): Promise<ManifestPreview> {
  return request<ManifestPreview>(`${basePath}/applications/${id}/manifest/preview`, {
    method: 'POST',
    body: JSON.stringify({ manifest: data.manifest, concurrencyToken }),
  });
}

export function applyPermissionManifest(id: string, data: PermissionManifestRequest, concurrencyToken?: number): Promise<ManifestApplyResult> {
  return request<ManifestApplyResult>(`${basePath}/applications/${id}/manifest`, {
    method: 'POST',
    body: JSON.stringify({ manifest: data.manifest, concurrencyToken }),
  });
}

export function previewRemotePermissionManifest(id: string, concurrencyToken?: number): Promise<ManifestPreview> {
  return request<ManifestPreview>(`${basePath}/applications/${id}/import/preview`, {
    method: 'POST',
    body: JSON.stringify({ concurrencyToken }),
  });
}

export function applyRemotePermissionManifest(id: string, concurrencyToken?: number): Promise<ManifestApplyResult> {
  return request<ManifestApplyResult>(`${basePath}/applications/${id}/import`, {
    method: 'POST',
    body: JSON.stringify({ concurrencyToken }),
  });
}

export function updateRegisteredApplication(id: string, data: UpdateRegisteredApplicationRequest): Promise<RegisteredApplication> {
  return request<RegisteredApplication>(`${basePath}/applications/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(data),
  });
}

export function addApplicationPermission(id: string, data: AddApplicationPermissionRequest): Promise<RegisteredApplication> {
  return request<RegisteredApplication>(`${basePath}/applications/${id}/permissions`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function changeApplicationLifecycle(id: string, data: ChangeLifecycleRequest): Promise<RegisteredApplication> {
  return request<RegisteredApplication>(`${basePath}/applications/${id}/lifecycle`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function transferApplicationOwnership(id: string, data: TransferApplicationOwnershipRequest): Promise<RegisteredApplication> {
  return request<RegisteredApplication>(`${basePath}/applications/${id}/ownership`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function addDelegatedMaintainer(id: string, data: AddDelegatedMaintainerRequest): Promise<RegisteredApplication> {
  return request<RegisteredApplication>(`${basePath}/applications/${id}/maintainers`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function removeDelegatedMaintainer(id: string, principalId: string, concurrencyToken?: number): Promise<RegisteredApplication> {
  const query = concurrencyToken === undefined ? '' : `?concurrencyToken=${concurrencyToken}`;
  return request<RegisteredApplication>(`${basePath}/applications/${id}/maintainers/${encodeURIComponent(principalId)}${query}`, {
    method: 'DELETE',
  });
}

export function getAssignablePermissionCatalog(params?: PaginationParams): Promise<PaginatedResponse<AssignablePermissionCatalogItem>> {
  return request<PaginatedResponse<AssignablePermissionCatalogItem>>(`${basePath}/catalog`, {}, params);
}

export function getApplicationPermissionHistory(params?: { applicationIdentifier?: string }): Promise<ApplicationPermissionHistory> {
  return request<ApplicationPermissionHistory>(`${basePath}/history`, {}, params);
}

export function getApplicationPermissionDiagnostics(): Promise<PermissionDiagnostics> {
  return request<PermissionDiagnostics>(`${basePath}/diagnostics`);
}

export function updateRemovedPermissionReplacement(permissionId: string, data: ReplacementGuidanceRequest): Promise<RemovedPermissionDetail> {
  return request<RemovedPermissionDetail>(`${basePath}/permissions/${permissionId}/replacement`, {
    method: 'PATCH',
    body: JSON.stringify(data),
  });
}
