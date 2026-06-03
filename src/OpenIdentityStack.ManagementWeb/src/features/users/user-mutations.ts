import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  assignUserRole,
  createUser,
  deleteUser,
  disableUser,
  enableUser,
  linkUserUpstreamIdentity,
  resetUserPassword,
  unassignUserRole,
  unlinkUserUpstreamIdentity,
  updateUser,
  type CreateUserRequest,
  type LinkUpstreamIdentityRequest,
} from './users-api';

export function useUpdateUserMutation(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (displayName: string) => updateUser(userId, { displayName }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  });
}

export function useCreateUserMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateUserRequest) => createUser(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  });
}

export function useDisableUserMutation(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => disableUser(userId, { reason: 'Disabled from Management Web' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  });
}

export function useEnableUserMutation(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => enableUser(userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  });
}

export function useDeleteUserMutation(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => deleteUser(userId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  });
}

export function useResetUserPasswordMutation(userId: string) {
  return useMutation({
    mutationFn: (newPassword: string) => resetUserPassword(userId, { newPassword }),
  });
}

export function useAssignRoleMutation(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (roleId: string) => assignUserRole(userId, roleId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users', userId, 'roles'] }),
  });
}

export function useUnassignRoleMutation(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (roleId: string) => unassignUserRole(userId, roleId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users', userId, 'roles'] }),
  });
}

export function useLinkUpstreamIdentityMutation(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: LinkUpstreamIdentityRequest) => linkUserUpstreamIdentity(userId, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users', userId, 'upstream-identities'] }),
  });
}

export function useUnlinkUpstreamIdentityMutation(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (providerId: string) => unlinkUserUpstreamIdentity(userId, providerId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users', userId, 'upstream-identities'] }),
  });
}
