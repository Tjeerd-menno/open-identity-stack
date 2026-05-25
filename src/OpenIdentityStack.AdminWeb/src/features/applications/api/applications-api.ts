import { apiClient } from '@/lib/api/client';
import type {
  Application,
  ApplicationCreatedResponse,
  ApplicationCredential,
  ApplicationListParams,
  ApplicationListResponse,
  ApplicationProfilePolicy,
  AddApplicationCertificateRequest,
  AddApplicationCertificateResponse,
  AddApplicationSecretRequest,
  AddApplicationSecretResponse,
  ConfigureApplicationOAuthRequest,
  CreateApplicationRequest,
  UpdateApplicationMetadataRequest,
} from '@/types';

export async function getApplications(
  params?: ApplicationListParams
): Promise<ApplicationListResponse> {
  return await apiClient.get<ApplicationListResponse>('/api/admin/applications', params);
}

export async function getApplication(applicationId: string): Promise<Application> {
  return await apiClient.get<Application>(`/api/admin/applications/${applicationId}`);
}

export async function getApplicationProfilePolicies(): Promise<ApplicationProfilePolicy[]> {
  return await apiClient.get<ApplicationProfilePolicy[]>('/api/admin/applications/policies/profiles');
}

export async function createApplication(
  data: CreateApplicationRequest
): Promise<ApplicationCreatedResponse> {
  return await apiClient.post<ApplicationCreatedResponse>('/api/admin/applications', data);
}

export async function updateApplicationMetadata(
  applicationId: string,
  data: UpdateApplicationMetadataRequest
): Promise<Application> {
  return await apiClient.patch<Application>(`/api/admin/applications/${applicationId}`, data);
}

export async function configureApplicationOAuth(
  applicationId: string,
  data: ConfigureApplicationOAuthRequest
): Promise<Application> {
  return await apiClient.put<Application>(`/api/admin/applications/${applicationId}/oauth`, data);
}

export async function disableApplication(applicationId: string): Promise<Application> {
  return await apiClient.post<Application>(`/api/admin/applications/${applicationId}/disable`);
}

export async function enableApplication(applicationId: string): Promise<Application> {
  return await apiClient.post<Application>(`/api/admin/applications/${applicationId}/enable`);
}

export async function deleteApplication(applicationId: string): Promise<void> {
  return await apiClient.delete<void>(`/api/admin/applications/${applicationId}`);
}

export async function listApplicationCredentials(
  applicationId: string
): Promise<ApplicationCredential[]> {
  return await apiClient.get<ApplicationCredential[]>(
    `/api/admin/applications/${applicationId}/credentials`
  );
}

export async function addApplicationSecret(
  applicationId: string,
  data: AddApplicationSecretRequest
): Promise<AddApplicationSecretResponse> {
  return await apiClient.post<AddApplicationSecretResponse>(
    `/api/admin/applications/${applicationId}/credentials/client-secrets`,
    data
  );
}

export async function addApplicationCertificate(
  applicationId: string,
  data: AddApplicationCertificateRequest
): Promise<AddApplicationCertificateResponse> {
  return await apiClient.post<AddApplicationCertificateResponse>(
    `/api/admin/applications/${applicationId}/credentials/certificates`,
    data
  );
}

export async function revokeApplicationCredential(
  applicationId: string,
  credentialId: string
): Promise<void> {
  return await apiClient.delete<void>(
    `/api/admin/applications/${applicationId}/credentials/${credentialId}`
  );
}
