import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addApplicationCertificate,
  addApplicationSecret,
  configureApplicationOAuth,
  createApplication,
  deleteApplication,
  disableApplication,
  enableApplication,
  getApplication,
  getApplicationProfilePolicies,
  getApplications,
  listApplicationCredentials,
  revokeApplicationCredential,
  updateApplicationMetadata,
  type AddApplicationCertificateRequest,
  type AddApplicationSecretRequest,
  type Application,
  type ApplicationCredential,
  type ApplicationListParams,
  type ApplicationListResponse,
  type ApplicationProfilePolicy,
  type ConfigureApplicationOAuthRequest,
  type CreateApplicationRequest,
  type UpdateApplicationMetadataRequest,
} from './applications-api';

export const APPLICATIONS_QUERY_KEY = 'applications';

export function useApplications(params?: ApplicationListParams) {
  return useQuery<ApplicationListResponse>({
    queryKey: [APPLICATIONS_QUERY_KEY, params],
    queryFn: () => getApplications(params),
    staleTime: 30000,
    refetchOnMount: 'always',
  });
}

export function useApplication(applicationId: string | undefined) {
  return useQuery<Application>({
    queryKey: [APPLICATIONS_QUERY_KEY, applicationId],
    queryFn: () => getApplication(applicationId ?? ''),
    enabled: !!applicationId,
  });
}

export function useApplicationProfilePolicies() {
  return useQuery<ApplicationProfilePolicy[]>({
    queryKey: [APPLICATIONS_QUERY_KEY, 'profile-policies'],
    queryFn: getApplicationProfilePolicies,
    staleTime: 60000,
  });
}

export function useApplicationCredentials(applicationId: string | undefined) {
  return useQuery<ApplicationCredential[]>({
    queryKey: [APPLICATIONS_QUERY_KEY, applicationId, 'credentials'],
    queryFn: () => listApplicationCredentials(applicationId ?? ''),
    enabled: !!applicationId,
  });
}

export function useCreateApplication() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateApplicationRequest) => createApplication(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY] }),
  });
}

export function useUpdateApplication(applicationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateApplicationMetadataRequest) => updateApplicationMetadata(applicationId, data),
    onSuccess: () => invalidateApplicationQueries(queryClient, applicationId),
  });
}

export function useConfigureApplicationOAuth(applicationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: ConfigureApplicationOAuthRequest) => configureApplicationOAuth(applicationId, data),
    onSuccess: () => invalidateApplicationQueries(queryClient, applicationId),
  });
}

export function useDisableApplication(applicationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => disableApplication(applicationId),
    onSuccess: () => invalidateApplicationQueries(queryClient, applicationId),
  });
}

export function useEnableApplication(applicationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => enableApplication(applicationId),
    onSuccess: () => invalidateApplicationQueries(queryClient, applicationId),
  });
}

export function useDeleteApplication(applicationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => deleteApplication(applicationId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY] }),
  });
}

export function useAddApplicationSecret(applicationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: AddApplicationSecretRequest) => addApplicationSecret(applicationId, data),
    onSuccess: () => invalidateApplicationQueries(queryClient, applicationId),
  });
}

export function useAddApplicationCertificate(applicationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: AddApplicationCertificateRequest) => addApplicationCertificate(applicationId, data),
    onSuccess: () => invalidateApplicationQueries(queryClient, applicationId),
  });
}

export function useRevokeApplicationCredential(applicationId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (credentialId: string) => revokeApplicationCredential(applicationId, credentialId),
    onSuccess: () => invalidateApplicationQueries(queryClient, applicationId),
  });
}

type QueryInvalidator = {
  invalidateQueries: (options: { queryKey: unknown[] }) => unknown;
};

function invalidateApplicationQueries(queryClient: QueryInvalidator, applicationId: string) {
  queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY] });
  queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY, applicationId] });
  queryClient.invalidateQueries({ queryKey: [APPLICATIONS_QUERY_KEY, applicationId, 'credentials'] });
}
