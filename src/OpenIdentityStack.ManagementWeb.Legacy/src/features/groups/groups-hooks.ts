import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  addGroupMapping,
  addMemberToGroup,
  createGroup,
  deleteGroup,
  getGroup,
  getGroupMappings,
  getGroupMembers,
  getGroups,
  removeGroupMapping,
  removeMemberFromGroup,
  updateGroup,
  type AddGroupMappingRequest,
  type CreateGroupRequest,
  type Group,
  type GroupListParams,
  type GroupListItem,
  type GroupMapping,
  type GroupMappingsResponse,
  type GroupMember,
  type UpdateGroupRequest,
} from './groups-api';
import type { PaginatedResponse } from '@/lib/admin-api';

export const GROUPS_QUERY_KEY = 'groups';

export function useGroups(params?: GroupListParams) {
  return useQuery<PaginatedResponse<GroupListItem>>({
    queryKey: [GROUPS_QUERY_KEY, params],
    queryFn: () => getGroups(params),
    staleTime: 30000,
    refetchOnMount: 'always',
  });
}

export function useGroup(groupId: string | undefined) {
  return useQuery<Group>({
    queryKey: [GROUPS_QUERY_KEY, groupId],
    queryFn: () => getGroup(groupId ?? ''),
    enabled: !!groupId,
  });
}

export function useGroupMembers(groupId: string | undefined, page = 1, pageSize = 20) {
  return useQuery<PaginatedResponse<GroupMember>>({
    queryKey: [GROUPS_QUERY_KEY, groupId, 'members', page, pageSize],
    queryFn: () => getGroupMembers(groupId ?? '', { page, pageSize }),
    enabled: !!groupId,
  });
}

export function useGroupMappings(groupId: string | undefined) {
  return useQuery<GroupMappingsResponse>({
    queryKey: [GROUPS_QUERY_KEY, groupId, 'mappings'],
    queryFn: () => getGroupMappings(groupId ?? ''),
    enabled: !!groupId,
  });
}

export function useCreateGroup() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateGroupRequest) => createGroup(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [GROUPS_QUERY_KEY] }),
  });
}

export function useUpdateGroup(groupId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateGroupRequest) => updateGroup(groupId, data),
    onSuccess: () => invalidateGroupQueries(queryClient, groupId),
  });
}

export function useDeleteGroup(groupId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => deleteGroup(groupId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [GROUPS_QUERY_KEY] }),
  });
}

export function useAddGroupMember(groupId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (userId: string) => addMemberToGroup(groupId, userId),
    onSuccess: () => invalidateGroupQueries(queryClient, groupId),
  });
}

export function useRemoveGroupMember(groupId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (userId: string) => removeMemberFromGroup(groupId, userId),
    onSuccess: () => invalidateGroupQueries(queryClient, groupId),
  });
}

export function useAddGroupMapping(groupId: string) {
  const queryClient = useQueryClient();

  return useMutation<GroupMapping, Error, AddGroupMappingRequest>({
    mutationFn: (data) => addGroupMapping(groupId, data),
    onSuccess: () => invalidateGroupQueries(queryClient, groupId),
  });
}

export function useRemoveGroupMapping(groupId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (mappingId: string) => removeGroupMapping(groupId, mappingId),
    onSuccess: () => invalidateGroupQueries(queryClient, groupId),
  });
}

type QueryInvalidator = {
  invalidateQueries: (options: { queryKey: unknown[] }) => unknown;
};

function invalidateGroupQueries(queryClient: QueryInvalidator, groupId: string) {
  queryClient.invalidateQueries({ queryKey: [GROUPS_QUERY_KEY] });
  queryClient.invalidateQueries({ queryKey: [GROUPS_QUERY_KEY, groupId] });
  queryClient.invalidateQueries({ queryKey: [GROUPS_QUERY_KEY, groupId, 'members'] });
  queryClient.invalidateQueries({ queryKey: [GROUPS_QUERY_KEY, groupId, 'mappings'] });
}
