import { apiClient } from '@/lib/api/client';
import type {
  Group,
  GroupListItem,
  GroupMember,
  GroupMapping,
  PaginatedResponse,
  CreateGroupRequest,
  UpdateGroupRequest,
  AddGroupMappingRequest,
} from '@/types';

/**
 * Fetches a paginated list of groups
 * @param page - Page number (1-indexed)
 * @param pageSize - Number of items per page
 * @param search - Optional search query
 * @returns Paginated list of groups
 */
export async function getGroups(
  page = 1,
  pageSize = 20,
  search?: string
): Promise<PaginatedResponse<GroupListItem>> {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
  });
  
  if (search) {
    params.append('search', search);
  }
  
  return await apiClient.get<PaginatedResponse<GroupListItem>>(
    `/api/admin/groups?${params.toString()}`
  );
}

/**
 * Fetches details of a specific group
 * @param id - Group ID
 * @returns Group details
 */
export async function getGroup(id: string): Promise<Group> {
  return await apiClient.get<Group>(`/api/admin/groups/${id}`);
}

/**
 * Creates a new group
 * @param data - Group creation data
 * @returns Created group
 */
export async function createGroup(data: CreateGroupRequest): Promise<Group> {
  return await apiClient.post<Group>('/api/admin/groups', data);
}

/**
 * Updates an existing group
 * @param id - Group ID
 * @param data - Group update data
 * @returns Updated group
 */
export async function updateGroup(
  id: string,
  data: UpdateGroupRequest
): Promise<Group> {
  return await apiClient.patch<Group>(`/api/admin/groups/${id}`, data);
}

/**
 * Deletes a group
 * @param id - Group ID
 */
export async function deleteGroup(id: string): Promise<void> {
  await apiClient.delete(`/api/admin/groups/${id}`);
}

/**
 * Fetches members of a specific group
 * @param groupId - Group ID
 * @param page - Page number
 * @param pageSize - Number of items per page
 * @returns Paginated list of group members
 */
export async function getGroupMembers(
  groupId: string,
  page = 1,
  pageSize = 20
): Promise<PaginatedResponse<GroupMember>> {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
  });
  
  return await apiClient.get<PaginatedResponse<GroupMember>>(
    `/api/admin/groups/${groupId}/members?${params.toString()}`
  );
}

/**
 * Adds a user to a group
 * @param groupId - Group ID
 * @param userId - User ID to add
 */
export async function addMemberToGroup(
  groupId: string,
  userId: string
): Promise<void> {
  await apiClient.post(`/api/admin/groups/${groupId}/members/${userId}`);
}

/**
 * Removes a user from a group
 * @param groupId - Group ID
 * @param userId - User ID to remove
 */
export async function removeMemberFromGroup(
  groupId: string,
  userId: string
): Promise<void> {
  await apiClient.delete(`/api/admin/groups/${groupId}/members/${userId}`);
}

/**
 * Fetches mappings (roles/claims) for a specific group
 * @param groupId - Group ID
 * @returns List of group mappings
 */
export async function getGroupMappings(
  groupId: string
): Promise<{ items: GroupMapping[] }> {
  return await apiClient.get<{ items: GroupMapping[] }>(
    `/api/admin/groups/${groupId}/mappings`
  );
}

/**
 * Adds a mapping (role/claim) to a group
 * @param groupId - Group ID
 * @param data - Mapping data
 * @returns Created mapping
 */
export async function addGroupMapping(
  groupId: string,
  data: AddGroupMappingRequest
): Promise<GroupMapping> {
  return await apiClient.post<GroupMapping>(
    `/api/admin/groups/${groupId}/mappings`,
    data
  );
}

/**
 * Removes a mapping from a group
 * @param groupId - Group ID
 * @param mappingId - Mapping ID to remove
 */
export async function removeGroupMapping(
  groupId: string,
  mappingId: string
): Promise<void> {
  await apiClient.delete(`/api/admin/groups/${groupId}/mappings/${mappingId}`);
}
