import type { AdminApiClient, PaginatedResponse, RequestParams } from './index';

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
  addedAt?: string;
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

export type GroupsContract = {
  getGroups: (params?: GroupListParams) => Promise<PaginatedResponse<GroupListItem>>;
  getGroup: (groupId: string) => Promise<Group>;
  createGroup: (data: CreateGroupRequest) => Promise<Group>;
  updateGroup: (groupId: string, data: UpdateGroupRequest) => Promise<Group>;
  deleteGroup: (groupId: string) => Promise<void>;
  getGroupMembers: (groupId: string, params?: RequestParams) => Promise<PaginatedResponse<GroupMember>>;
  addMemberToGroup: (groupId: string, userId: string) => Promise<void>;
  removeMemberFromGroup: (groupId: string, userId: string) => Promise<void>;
  getGroupMappings: (groupId: string) => Promise<GroupMappingsResponse>;
  addGroupMapping: (groupId: string, data: AddGroupMappingRequest) => Promise<GroupMapping>;
  removeGroupMapping: (groupId: string, mappingId: string) => Promise<void>;
};

export function createGroupsContract(client: AdminApiClient): GroupsContract {
  return {
    getGroups: (params?: GroupListParams) => client.get<PaginatedResponse<GroupListItem>>('/api/admin/groups', params),
    getGroup: (groupId) => client.get<Group>(`/api/admin/groups/${groupId}`),
    createGroup: (data) => client.post<Group>('/api/admin/groups', data),
    updateGroup: (groupId, data) => client.patch<Group>(`/api/admin/groups/${groupId}`, data),
    deleteGroup: (groupId) => client.delete<void>(`/api/admin/groups/${groupId}`),
    getGroupMembers: async (groupId, params) => {
      // The members endpoint returns a user list whose id field is the user id; map it to
      // userId so callers (row keys, member removal) get a defined identifier.
      const response = await client.get<PaginatedResponse<GroupMember>>(
        `/api/admin/groups/${groupId}/members`,
        params
      );
      return {
        ...response,
        items: response.items.map((member) => ({ ...member, userId: member.userId ?? member.id ?? '' })),
      };
    },
    addMemberToGroup: (groupId, userId) => client.post<void>(`/api/admin/groups/${groupId}/members/${userId}`),
    removeMemberFromGroup: (groupId, userId) =>
      client.delete<void>(`/api/admin/groups/${groupId}/members/${userId}`),
    getGroupMappings: (groupId) => client.get<GroupMappingsResponse>(`/api/admin/groups/${groupId}/mappings`),
    addGroupMapping: (groupId, data) =>
      client.post<GroupMapping>(`/api/admin/groups/${groupId}/mappings`, data),
    removeGroupMapping: (groupId, mappingId) =>
      client.delete<void>(`/api/admin/groups/${groupId}/mappings/${mappingId}`),
  };
}
