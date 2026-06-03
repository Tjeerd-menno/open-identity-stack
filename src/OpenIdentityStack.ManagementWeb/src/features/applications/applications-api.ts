import { request, type PaginatedResponse } from '@/lib/admin-api';

export const ApplicationProfile = {
  Web: 'Web',
  SinglePage: 'SinglePage',
  Native: 'Native',
  MachineToMachine: 'MachineToMachine',
  Device: 'Device',
  Custom: 'Custom',
} as const;
export type ApplicationProfile = typeof ApplicationProfile[keyof typeof ApplicationProfile];

export const ApplicationClientType = {
  Confidential: 'Confidential',
  Public: 'Public',
} as const;
export type ApplicationClientType = typeof ApplicationClientType[keyof typeof ApplicationClientType];

export const ApplicationOptionAvailability = {
  Hidden: 'Hidden',
  ReadOnly: 'ReadOnly',
  Available: 'Available',
  Advanced: 'Advanced',
} as const;
export type ApplicationOptionAvailability =
  typeof ApplicationOptionAvailability[keyof typeof ApplicationOptionAvailability];

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

export function getApplications(params?: ApplicationListParams): Promise<ApplicationListResponse> {
  return request<ApplicationListResponse>('/api/admin/applications', {}, params);
}

export function getApplication(applicationId: string): Promise<Application> {
  return request<Application>(`/api/admin/applications/${applicationId}`);
}

export function getApplicationProfilePolicies(): Promise<ApplicationProfilePolicy[]> {
  return request<ApplicationProfilePolicy[]>('/api/admin/applications/policies/profiles');
}

export function createApplication(data: CreateApplicationRequest): Promise<ApplicationCreatedResponse> {
  return request<ApplicationCreatedResponse>('/api/admin/applications', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updateApplicationMetadata(
  applicationId: string,
  data: UpdateApplicationMetadataRequest
): Promise<Application> {
  return request<Application>(`/api/admin/applications/${applicationId}`, {
    method: 'PATCH',
    body: JSON.stringify(data),
  });
}

export function configureApplicationOAuth(
  applicationId: string,
  data: ConfigureApplicationOAuthRequest
): Promise<Application> {
  return request<Application>(`/api/admin/applications/${applicationId}/oauth`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

export function disableApplication(applicationId: string): Promise<Application> {
  return request<Application>(`/api/admin/applications/${applicationId}/disable`, {
    method: 'POST',
  });
}

export function enableApplication(applicationId: string): Promise<Application> {
  return request<Application>(`/api/admin/applications/${applicationId}/enable`, {
    method: 'POST',
  });
}

export function deleteApplication(applicationId: string): Promise<void> {
  return request<void>(`/api/admin/applications/${applicationId}`, {
    method: 'DELETE',
  });
}

export function listApplicationCredentials(applicationId: string): Promise<ApplicationCredential[]> {
  return request<ApplicationCredential[]>(`/api/admin/applications/${applicationId}/credentials`);
}

export function addApplicationSecret(
  applicationId: string,
  data: AddApplicationSecretRequest
): Promise<AddApplicationSecretResponse> {
  return request<AddApplicationSecretResponse>(
    `/api/admin/applications/${applicationId}/credentials/client-secrets`,
    {
      method: 'POST',
      body: JSON.stringify(data),
    }
  );
}

export function addApplicationCertificate(
  applicationId: string,
  data: AddApplicationCertificateRequest
): Promise<AddApplicationCertificateResponse> {
  return request<AddApplicationCertificateResponse>(
    `/api/admin/applications/${applicationId}/credentials/certificates`,
    {
      method: 'POST',
      body: JSON.stringify(data),
    }
  );
}

export function revokeApplicationCredential(applicationId: string, credentialId: string): Promise<void> {
  return request<void>(`/api/admin/applications/${applicationId}/credentials/${credentialId}`, {
    method: 'DELETE',
  });
}
