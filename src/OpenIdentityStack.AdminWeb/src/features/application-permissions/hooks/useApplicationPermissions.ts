import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addDelegatedMaintainer,
  addApplicationPermission,
  changeApplicationLifecycle,
  getAssignablePermissionCatalog,
  getPermissionDependencies,
  getRegisteredApplication,
  getRegisteredApplications,
  importPermissionManifestFromEndpoint,
  registerPermissionManifest,
  removeDelegatedMaintainer,
  transferApplicationOwnership,
  updateRegisteredApplication,
} from '../api';
import type {
  AddDelegatedMaintainerRequest,
  AddApplicationPermissionRequest,
  ChangeLifecycleRequest,
  PaginationParams,
  PermissionManifestRequest,
  TransferApplicationOwnershipRequest,
  UpdateRegisteredApplicationRequest,
} from '@/types';

export const APPLICATION_PERMISSIONS_QUERY_KEY = 'application-permissions';

export function useRegisteredApplications(params?: PaginationParams) {
  return useQuery({
    queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY, 'applications', params],
    queryFn: () => getRegisteredApplications(params),
    staleTime: 30000,
  });
}

export function useRegisteredApplication(id: string) {
  return useQuery({
    queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY, 'application', id],
    queryFn: () => getRegisteredApplication(id),
    enabled: id.length > 0,
  });
}

export function useAssignablePermissionCatalog(params?: PaginationParams) {
  return useQuery({
    queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY, 'catalog', params],
    queryFn: () => getAssignablePermissionCatalog(params),
    staleTime: 30000,
  });
}

export function usePermissionDependencies(permissionId: string) {
  return useQuery({
    queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY, 'dependencies', permissionId],
    queryFn: () => getPermissionDependencies(permissionId),
    enabled: permissionId.length > 0,
  });
}

export function useRegisterPermissionManifest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: PermissionManifestRequest) => registerPermissionManifest(data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useImportPermissionManifestFromEndpoint() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (endpoint: string) => importPermissionManifestFromEndpoint(endpoint),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useUpdateRegisteredApplication(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateRegisteredApplicationRequest) => updateRegisteredApplication(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useAddApplicationPermission(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: AddApplicationPermissionRequest) => addApplicationPermission(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useChangeApplicationLifecycle(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ChangeLifecycleRequest) => changeApplicationLifecycle(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useTransferApplicationOwnership(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: TransferApplicationOwnershipRequest) => transferApplicationOwnership(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useMaintainerMutations(id: string) {
  const queryClient = useQueryClient();
  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: [APPLICATION_PERMISSIONS_QUERY_KEY] });
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
