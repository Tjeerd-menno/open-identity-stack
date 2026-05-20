import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addDelegatedMaintainer,
  addServicePermission,
  changePermissionLifecycle,
  changeServiceLifecycle,
  getAssignablePermissionCatalog,
  getPermissionDependencies,
  getRegisteredService,
  getRegisteredServices,
  registerService,
  removeDelegatedMaintainer,
  transferServiceOwnership,
  updateRegisteredService,
} from '../api';
import type {
  AddDelegatedMaintainerRequest,
  AddServicePermissionRequest,
  ChangeLifecycleRequest,
  PaginationParams,
  RegisterServiceRequest,
  TransferServiceOwnershipRequest,
  UpdateRegisteredServiceRequest,
} from '@/types';

export const SERVICE_PERMISSIONS_QUERY_KEY = 'service-permissions';

export function useRegisteredServices(params?: PaginationParams) {
  return useQuery({
    queryKey: [SERVICE_PERMISSIONS_QUERY_KEY, 'services', params],
    queryFn: () => getRegisteredServices(params),
    staleTime: 30000,
  });
}

export function useRegisteredService(id: string) {
  return useQuery({
    queryKey: [SERVICE_PERMISSIONS_QUERY_KEY, 'service', id],
    queryFn: () => getRegisteredService(id),
    enabled: id.length > 0,
  });
}

export function useAssignablePermissionCatalog(params?: PaginationParams) {
  return useQuery({
    queryKey: [SERVICE_PERMISSIONS_QUERY_KEY, 'catalog', params],
    queryFn: () => getAssignablePermissionCatalog(params),
    staleTime: 30000,
  });
}

export function usePermissionDependencies(permissionId: string) {
  return useQuery({
    queryKey: [SERVICE_PERMISSIONS_QUERY_KEY, 'dependencies', permissionId],
    queryFn: () => getPermissionDependencies(permissionId),
    enabled: permissionId.length > 0,
  });
}

export function useRegisterService() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: RegisterServiceRequest) => registerService(data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [SERVICE_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useUpdateRegisteredService(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateRegisteredServiceRequest) => updateRegisteredService(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [SERVICE_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useAddServicePermission(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: AddServicePermissionRequest) => addServicePermission(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [SERVICE_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useChangeServiceLifecycle(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ChangeLifecycleRequest) => changeServiceLifecycle(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [SERVICE_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useChangePermissionLifecycle(permissionId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ChangeLifecycleRequest) => changePermissionLifecycle(permissionId, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [SERVICE_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useTransferServiceOwnership(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: TransferServiceOwnershipRequest) => transferServiceOwnership(id, data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [SERVICE_PERMISSIONS_QUERY_KEY] });
    },
  });
}

export function useMaintainerMutations(id: string) {
  const queryClient = useQueryClient();
  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: [SERVICE_PERMISSIONS_QUERY_KEY] });
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
