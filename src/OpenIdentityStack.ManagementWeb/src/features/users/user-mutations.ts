import { useMutation, useQueryClient } from '@tanstack/react-query';
import { assignRole, createUser, disableUser, updateUser, type CreateUserRequest } from '@/lib/admin-api';

export function useUpdateUserMutation(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (displayName: string) => updateUser(userId, displayName),
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
    mutationFn: () => disableUser(userId, 'Disabled from Management Web'),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  });
}

export function useAssignRoleMutation(userId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (roleId: string) => assignRole(userId, roleId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users', userId, 'roles'] }),
  });
}
