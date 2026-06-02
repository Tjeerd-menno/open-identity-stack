import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addApplicationPermission,
  addDelegatedMaintainer,
  changeApplicationLifecycle,
  getApplicationPermissionDiagnostics,
  getApplicationPermissionHistory,
  getAssignablePermissionCatalog,
  getRegisteredApplication,
  getRegisteredApplications,
  importPermissionManifestFromEndpoint,
  registerPermissionManifest,
  removeDelegatedMaintainer,
  transferApplicationOwnership,
  type AddApplicationPermissionRequest,
  type AddDelegatedMaintainerRequest,
  type ChangeLifecycleRequest,
  type PaginationParams,
  type PermissionManifestRequest,
  type TransferApplicationOwnershipRequest,
} from './application-permissions-api';

const queryKey = 'application-permissions';

export function useRegisteredApplications(params: PaginationParams) {
  return useQuery({
    queryKey: [queryKey, 'applications', params],
    queryFn: () => getRegisteredApplications(params),
    staleTime: 30_000,
  });
}

export function useRegisteredApplication(id: string) {
  return useQuery({
    queryKey: [queryKey, 'application', id],
    queryFn: () => getRegisteredApplication(id),
    enabled: id.length > 0,
  });
}

export function useRegisterPermissionManifest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: PermissionManifestRequest) => registerPermissionManifest(data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [queryKey] });
    },
  });
}

export function useImportPermissionManifestFromEndpoint() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (endpoint: string) => importPermissionManifestFromEndpoint(endpoint),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [queryKey] });
    },
  });
}

export function useChangeApplicationLifecycle(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ChangeLifecycleRequest) => changeApplicationLifecycle(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [queryKey] });
    },
  });
}

export function useTransferApplicationOwnership(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: TransferApplicationOwnershipRequest) => transferApplicationOwnership(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [queryKey] });
    },
  });
}

export function useAddApplicationPermission(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: AddApplicationPermissionRequest) => addApplicationPermission(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [queryKey] });
    },
  });
}

export function useMaintainerMutations(id: string) {
  const queryClient = useQueryClient();
  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: [queryKey] });
  };

  return {
    add: useMutation({
      mutationFn: (data: AddDelegatedMaintainerRequest) => addDelegatedMaintainer(id, data),
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: ({ principalId, concurrencyToken }: { principalId: string; concurrencyToken?: number }) =>
        removeDelegatedMaintainer(id, principalId, concurrencyToken),
      onSuccess: invalidate,
    }),
  };
}

export function useAssignablePermissionCatalog(params: PaginationParams) {
  return useQuery({
    queryKey: [queryKey, 'catalog', params],
    queryFn: () => getAssignablePermissionCatalog(params),
    staleTime: 30_000,
  });
}

export function useApplicationPermissionHistory(applicationIdentifier?: string) {
  return useQuery({
    queryKey: [queryKey, 'history', applicationIdentifier],
    queryFn: () => getApplicationPermissionHistory({ applicationIdentifier }),
    enabled: Boolean(applicationIdentifier),
    staleTime: 30_000,
  });
}

export function useApplicationPermissionDiagnostics() {
  return useQuery({
    queryKey: [queryKey, 'diagnostics'],
    queryFn: getApplicationPermissionDiagnostics,
    staleTime: 30_000,
  });
}
