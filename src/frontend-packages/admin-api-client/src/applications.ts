import type { AdminApiClient, PaginatedResponse } from './index';

export const ApplicationProfile = {
  Web: 'Web',
  SinglePage: 'SinglePage',
  Native: 'Native',
  MachineToMachine: 'MachineToMachine',
  Device: 'Device',
  Custom: 'Custom',
} as const;
export type ApplicationProfile = (typeof ApplicationProfile)[keyof typeof ApplicationProfile];

export const ApplicationClientType = {
  Confidential: 'Confidential',
  Public: 'Public',
} as const;
export type ApplicationClientType = (typeof ApplicationClientType)[keyof typeof ApplicationClientType];

export const ApplicationOptionAvailability = {
  Hidden: 'Hidden',
  ReadOnly: 'ReadOnly',
  Available: 'Available',
  Advanced: 'Advanced',
} as const;
export type ApplicationOptionAvailability = (typeof ApplicationOptionAvailability)[keyof typeof ApplicationOptionAvailability];

export type ApplicationStatus = 'Active' | 'Disabled';

export type ApplicationProfilePolicy = {
  applicationProfile: ApplicationProfile;
  isSelectable: boolean;
  unavailabilityReason: string | null;
  defaultClientProfile: ApplicationClientType;
  allowedClientProfiles: ApplicationClientType[];
  allowedGrantTypes: string[];
  defaultGrantTypes: string[];
  options: Record<string, ApplicationOptionAvailability>;
  requirePkce: boolean;
  defaultRequirePkce: boolean;
  defaultRequireConsent: boolean;
  requiresRedirectUris: boolean;
};

export type Application = {
  id: string;
  clientId: string;
  displayName: string;
  description: string | null;
  profile: ApplicationProfile;
  clientType: ApplicationClientType;
  status: ApplicationStatus;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  allowedScopes: string[];
  allowedGrantTypes: string[];
  requirePkce: boolean;
  requireConsent: boolean;
  credentialCount: number;
  certificateCount: number;
  requiresMigrationReview: boolean;
  migrationSource: string | null;
  createdAt: string;
  modifiedAt: string | null;
};

export type ApplicationListItem = {
  id: string;
  clientId: string;
  displayName: string;
  profile: ApplicationProfile;
  clientType: ApplicationClientType;
  status: ApplicationStatus;
  allowedGrantTypes: string[];
  credentialCount: number;
  createdAt: string;
  modifiedAt: string | null;
};

export type ApplicationListResponse = PaginatedResponse<ApplicationListItem>;

export type ApplicationListParams = {
  page?: number;
  pageSize?: number;
  search?: string;
  profile?: ApplicationProfile;
  status?: ApplicationStatus;
  clientType?: ApplicationClientType;
};

export type CreateApplicationRequest = {
  clientId: string;
  displayName: string;
  description?: string | null;
  profile: ApplicationProfile;
  clientType: ApplicationClientType;
  allowedGrantTypes: string[];
  allowedScopes: string[];
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  requirePkce: boolean;
  requireConsent: boolean;
};

export type UpdateApplicationMetadataRequest = {
  displayName: string;
  description?: string | null;
};

export type ConfigureApplicationOAuthRequest = Omit<
  CreateApplicationRequest,
  'clientId' | 'displayName' | 'description'
>;

export type ApplicationCreatedResponse = {
  id: string;
  clientId: string;
  displayName: string;
  profile: ApplicationProfile;
  clientType: ApplicationClientType;
  status: ApplicationStatus;
  initialSecret: string | null;
  createdAt: string;
};

export type ApplicationCredentialType = 'ClientSecret' | 'X509Certificate';

export type ApplicationCredential = {
  id: string;
  applicationId: string;
  type: ApplicationCredentialType;
  thumbprint: string | null;
  subject: string | null;
  description: string | null;
  expiresAt: string | null;
  createdAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
};

export type AddApplicationSecretRequest = {
  description?: string | null;
  expiresAt?: string | null;
  revokeExisting: boolean;
};

export type AddApplicationSecretResponse = {
  credentialId: string;
  clientSecret: string;
};

export type AddApplicationCertificateRequest = {
  thumbprint: string;
  subject?: string | null;
  description?: string | null;
  expiresAt?: string | null;
};

export type AddApplicationCertificateResponse = {
  credentialId: string;
};

export type ApplicationsContract = {
  listProtectedResources: () => Promise<ProtectedResource[]>;
  createProtectedResource: (data: ResourceConfiguration) => Promise<ProtectedResource>;
  configureProtectedResource: (resourceId: string, data: ResourceConfiguration) => Promise<ProtectedResource>;
  listClientResourceGrants: (applicationId: string) => Promise<ClientResourceGrant[]>;
  configureClientResourceGrant: (applicationId: string, resourceId: string, data: ClientResourceGrantConfiguration) => Promise<ClientResourceGrant>;
  getApplications: (params?: ApplicationListParams) => Promise<ApplicationListResponse>;
  getApplication: (applicationId: string) => Promise<Application>;
  getApplicationProfilePolicies: () => Promise<ApplicationProfilePolicy[]>;
  createApplication: (data: CreateApplicationRequest) => Promise<ApplicationCreatedResponse>;
  updateApplicationMetadata: (applicationId: string, data: UpdateApplicationMetadataRequest) => Promise<Application>;
  configureApplicationOAuth: (applicationId: string, data: ConfigureApplicationOAuthRequest) => Promise<Application>;
  disableApplication: (applicationId: string) => Promise<Application>;
  enableApplication: (applicationId: string) => Promise<Application>;
  deleteApplication: (applicationId: string) => Promise<void>;
  listApplicationCredentials: (applicationId: string) => Promise<ApplicationCredential[]>;
  addApplicationSecret: (applicationId: string, data: AddApplicationSecretRequest) => Promise<AddApplicationSecretResponse>;
  addApplicationCertificate: (applicationId: string, data: AddApplicationCertificateRequest) => Promise<AddApplicationCertificateResponse>;
  revokeApplicationCredential: (applicationId: string, credentialId: string) => Promise<void>;
};

export function createApplicationsContract(client: AdminApiClient): ApplicationsContract {
  return {
    listProtectedResources: () => client.get<ProtectedResource[]>('/api/admin/applications/resources'),
    createProtectedResource: (data) => client.post<ProtectedResource>('/api/admin/applications/resources', data),
    configureProtectedResource: (resourceId, data) => client.put<ProtectedResource>(`/api/admin/applications/resources/${resourceId}`, data),
    listClientResourceGrants: (applicationId) => client.get<ClientResourceGrant[]>(`/api/admin/applications/${applicationId}/resource-grants`),
    configureClientResourceGrant: (applicationId, resourceId, data) => client.put<ClientResourceGrant>(`/api/admin/applications/${applicationId}/resource-grants/${resourceId}`, data),
    getApplications: (params) =>
      client.get<ApplicationListResponse>('/api/admin/applications', params),
    getApplication: (applicationId) => client.get<Application>(`/api/admin/applications/${applicationId}`),
    getApplicationProfilePolicies: () =>
      client.get<ApplicationProfilePolicy[]>('/api/admin/applications/policies/profiles'),
    createApplication: (data) => client.post<ApplicationCreatedResponse>('/api/admin/applications', data),
    updateApplicationMetadata: (applicationId, data) =>
      client.patch<Application>(`/api/admin/applications/${applicationId}`, data),
    configureApplicationOAuth: (applicationId, data) =>
      client.put<Application>(`/api/admin/applications/${applicationId}/oauth`, data),
    disableApplication: (applicationId) =>
      client.post<Application>(`/api/admin/applications/${applicationId}/disable`),
    enableApplication: (applicationId) =>
      client.post<Application>(`/api/admin/applications/${applicationId}/enable`),
    deleteApplication: (applicationId) =>
      client.delete<void>(`/api/admin/applications/${applicationId}`),
    listApplicationCredentials: (applicationId) =>
      client.get<ApplicationCredential[]>(`/api/admin/applications/${applicationId}/credentials`),
    addApplicationSecret: (applicationId, data) =>
      client.post<AddApplicationSecretResponse>(`/api/admin/applications/${applicationId}/credentials/client-secrets`, data),
    addApplicationCertificate: (applicationId, data) =>
      client.post<AddApplicationCertificateResponse>(`/api/admin/applications/${applicationId}/credentials/certificates`, data),
    revokeApplicationCredential: (applicationId, credentialId) =>
      client.delete<void>(`/api/admin/applications/${applicationId}/credentials/${credentialId}`),
  };
}

export type ProtectedResource = {
  id: string;
  audience: string;
  scope: string;
  displayName: string;
  permissionNamespaces: string[];
  enabled: boolean;
  revision: number;
  isAdministrative: boolean;
};
export type ResourceConfiguration = Omit<ProtectedResource, 'id' | 'revision' | 'isAdministrative'> & { expectedRevision?: number };
export type ClientResourceGrant = {
  resourceId: string;
  delegatedPermissions: string[];
  applicationPermissions: string[];
  revision: number;
};
export type ClientResourceGrantConfiguration = Omit<ClientResourceGrant, 'resourceId' | 'revision'> & { expectedRevision?: number };
