import { adminApiClient } from '@/lib/api/client';
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
import { createApplicationsContract } from '@openidentitystack/admin-api-client';

const contract = createApplicationsContract(adminApiClient);

export async function getApplications(
  params?: ApplicationListParams
): Promise<ApplicationListResponse> {
  return contract.getApplications(params);
}

export async function getApplication(applicationId: string): Promise<Application> {
  return contract.getApplication(applicationId) as Promise<Application>;
}

export async function getApplicationProfilePolicies(): Promise<ApplicationProfilePolicy[]> {
  return contract.getApplicationProfilePolicies();
}

export async function createApplication(data: CreateApplicationRequest): Promise<ApplicationCreatedResponse> {
  return contract.createApplication(data);
}

export async function updateApplicationMetadata(
  applicationId: string,
  data: UpdateApplicationMetadataRequest
): Promise<Application> {
  return contract.updateApplicationMetadata(applicationId, data) as Promise<Application>;
}

export async function configureApplicationOAuth(
  applicationId: string,
  data: ConfigureApplicationOAuthRequest
): Promise<Application> {
  return contract.configureApplicationOAuth(applicationId, data) as Promise<Application>;
}

export async function disableApplication(applicationId: string): Promise<Application> {
  return contract.disableApplication(applicationId) as Promise<Application>;
}

export async function enableApplication(applicationId: string): Promise<Application> {
  return contract.enableApplication(applicationId) as Promise<Application>;
}

export async function deleteApplication(applicationId: string): Promise<void> {
  return contract.deleteApplication(applicationId);
}

export async function listApplicationCredentials(
  applicationId: string
): Promise<ApplicationCredential[]> {
  return contract.listApplicationCredentials(applicationId) as Promise<ApplicationCredential[]>;
}

export async function addApplicationSecret(
  applicationId: string,
  data: AddApplicationSecretRequest
): Promise<AddApplicationSecretResponse> {
  return contract.addApplicationSecret(applicationId, data);
}

export async function addApplicationCertificate(
  applicationId: string,
  data: AddApplicationCertificateRequest
): Promise<AddApplicationCertificateResponse> {
  return contract.addApplicationCertificate(applicationId, data);
}

export async function revokeApplicationCredential(
  applicationId: string,
  credentialId: string
): Promise<void> {
  return contract.revokeApplicationCredential(applicationId, credentialId);
}
