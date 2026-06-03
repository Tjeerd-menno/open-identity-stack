import { request, type PaginatedResponse } from '@/lib/admin-api';

export type Group = {
  id: string;
  name: string;
  description: string | null;
  parentGroupId?: string | null;
  memberCount?: number;
  mappingCount?: number;
  createdAt: string;
  modifiedAt?: string | null;
};

export type GroupListItem = Group;

export type GroupListParams = {
  page?: number;
  pageSize?: number;
  search?: string;
};

export type CreateGroupRequest = {
  name: string;
  description?: string | null;
};

export type UpdateGroupRequest = {
  name: string;
  description?: string | null;
};

export type GroupMember = {
  id?: string;
  userId: string;
  email: string;
  displayName: string;
};

export type GroupMappingType = 'Role' | 'Claim';

export type GroupMapping = {
  id: string;
  type: GroupMappingType;
  value: string;
  createdAt: string;
};

export type AddGroupMappingRequest = {
  type: GroupMappingType;
  value: string;
};

export type GroupMappingsResponse = {
  items: GroupMapping[];
};

export function getGroups(params?: GroupListParams): Promise<PaginatedResponse<GroupListItem>> {
  return request<PaginatedResponse<GroupListItem>>('/api/admin/groups', {}, params);
}

export function getGroup(groupId: string): Promise<Group> {
  return request<Group>(`/api/admin/groups/${groupId}`);
}

export function createGroup(data: CreateGroupRequest): Promise<Group> {
  return request<Group>('/api/admin/groups', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function updateGroup(groupId: string, data: UpdateGroupRequest): Promise<Group> {
  return request<Group>(`/api/admin/groups/${groupId}`, {
    method: 'PATCH',
    body: JSON.stringify(data),
  });
}

export function deleteGroup(groupId: string): Promise<void> {
  return request<void>(`/api/admin/groups/${groupId}`, {
    method: 'DELETE',
  });
}

export function getGroupMembers(
  groupId: string,
  params?: { page?: number; pageSize?: number }
): Promise<PaginatedResponse<GroupMember>> {
  return request<PaginatedResponse<GroupMember>>(`/api/admin/groups/${groupId}/members`, {}, params);
}

export function addMemberToGroup(groupId: string, userId: string): Promise<void> {
  return request<void>(`/api/admin/groups/${groupId}/members/${userId}`, {
    method: 'POST',
  });
}

export function removeMemberFromGroup(groupId: string, userId: string): Promise<void> {
  return request<void>(`/api/admin/groups/${groupId}/members/${userId}`, {
    method: 'DELETE',
  });
}

export function getGroupMappings(groupId: string): Promise<GroupMappingsResponse> {
  return request<GroupMappingsResponse>(`/api/admin/groups/${groupId}/mappings`);
}

export function addGroupMapping(groupId: string, data: AddGroupMappingRequest): Promise<GroupMapping> {
  return request<GroupMapping>(`/api/admin/groups/${groupId}/mappings`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

export function removeGroupMapping(groupId: string, mappingId: string): Promise<void> {
  return request<void>(`/api/admin/groups/${groupId}/mappings/${mappingId}`, {
    method: 'DELETE',
  });
}
