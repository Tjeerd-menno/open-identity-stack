import { adminApiClient } from '@/lib/admin-api';
import {
  createApplicationsContract,
  type Application as SharedApplication,
  type ApplicationCreatedResponse,
  type ApplicationCredential,
  type ApplicationListItem as SharedApplicationListItem,
  type ApplicationListParams,
  type ApplicationListResponse,
  type ApplicationProfile as SharedApplicationProfile,
  type ApplicationProfilePolicy,
  type AddApplicationCertificateRequest,
  type AddApplicationCertificateResponse,
  type AddApplicationSecretRequest,
  type AddApplicationSecretResponse,
  type ConfigureApplicationOAuthRequest,
  type CreateApplicationRequest,
  type UpdateApplicationMetadataRequest,
} from '@openidentitystack/admin-api-client';

const contract = createApplicationsContract(adminApiClient);

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
export type ApplicationOptionAvailability =
  (typeof ApplicationOptionAvailability)[keyof typeof ApplicationOptionAvailability];

export type Application = SharedApplication & {
  profile: SharedApplicationProfile;
  clientType: ApplicationClientType;
};
export type ApplicationListItem = SharedApplicationListItem;
export type ApplicationListResponse = SharedApplicationListResponse;
export type { ApplicationListParams, ApplicationProfilePolicy, CreateApplicationRequest };
export type { UpdateApplicationMetadataRequest, ConfigureApplicationOAuthRequest };
export type { ApplicationCreatedResponse, AddApplicationSecretRequest, AddApplicationSecretResponse };
export type { AddApplicationCertificateRequest, AddApplicationCertificateResponse };
export type { ApplicationCredential };

export function getApplications(params?: ApplicationListParams): Promise<ApplicationListResponse> {
  return contract.getApplications(params);
}

export function getApplication(applicationId: string): Promise<Application> {
  return contract.getApplication(applicationId);
}

export function getApplicationProfilePolicies(): Promise<ApplicationProfilePolicy[]> {
  return contract.getApplicationProfilePolicies();
}

export function createApplication(data: CreateApplicationRequest): Promise<ApplicationCreatedResponse> {
  return contract.createApplication(data);
}

export function updateApplicationMetadata(
  applicationId: string,
  data: UpdateApplicationMetadataRequest
): Promise<Application> {
  return contract.updateApplicationMetadata(applicationId, data);
}

export function configureApplicationOAuth(
  applicationId: string,
  data: ConfigureApplicationOAuthRequest
): Promise<Application> {
  return contract.configureApplicationOAuth(applicationId, data);
}

export function disableApplication(applicationId: string): Promise<Application> {
  return contract.disableApplication(applicationId);
}

export function enableApplication(applicationId: string): Promise<Application> {
  return contract.enableApplication(applicationId);
}

export function deleteApplication(applicationId: string): Promise<void> {
  return contract.deleteApplication(applicationId);
}

export function listApplicationCredentials(applicationId: string): Promise<ApplicationCredential[]> {
  return contract.listApplicationCredentials(applicationId);
}

export function addApplicationSecret(
  applicationId: string,
  data: AddApplicationSecretRequest
): Promise<AddApplicationSecretResponse> {
  return contract.addApplicationSecret(applicationId, data);
}

export function addApplicationCertificate(
  applicationId: string,
  data: AddApplicationCertificateRequest
): Promise<AddApplicationCertificateResponse> {
  return contract.addApplicationCertificate(applicationId, data);
}

export function revokeApplicationCredential(applicationId: string, credentialId: string): Promise<void> {
  return contract.revokeApplicationCredential(applicationId, credentialId);
}
