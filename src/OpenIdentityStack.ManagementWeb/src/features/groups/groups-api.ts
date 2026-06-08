import { adminApiClient } from '@/lib/admin-api';
import {
  createGroupsContract,
  type AddGroupMappingRequest,
  type CreateGroupRequest,
  type Group,
  type GroupListItem,
  type GroupListParams,
  type GroupMapping,
  type GroupMember,
  type GroupMappingsResponse,
  type UpdateGroupRequest,
  type PaginatedResponse,
  type RequestParams,
} from '@openidentitystack/admin-api-client';

const contract = createGroupsContract(adminApiClient);

export type { Group, GroupListItem, GroupListParams, GroupMember, GroupMapping, AddGroupMappingRequest, UpdateGroupRequest, PaginatedResponse };

export { type RequestParams as GroupMemberParams };

export function getGroups(params?: GroupListParams): Promise<PaginatedResponse<GroupListItem>> {
  return contract.getGroups(params);
}

export function getGroup(groupId: string): Promise<Group> {
  return contract.getGroup(groupId);
}

export function createGroup(data: CreateGroupRequest): Promise<Group> {
  return contract.createGroup(data);
}

export function updateGroup(groupId: string, data: UpdateGroupRequest): Promise<Group> {
  return contract.updateGroup(groupId, data);
}

export function deleteGroup(groupId: string): Promise<void> {
  return contract.deleteGroup(groupId);
}

export function getGroupMembers(
  groupId: string,
  params?: RequestParams
): Promise<PaginatedResponse<GroupMember>> {
  return contract.getGroupMembers(groupId, params as { page?: number; pageSize?: number } | undefined);
}

export function addMemberToGroup(groupId: string, userId: string): Promise<void> {
  return contract.addMemberToGroup(groupId, userId);
}

export function removeMemberFromGroup(groupId: string, userId: string): Promise<void> {
  return contract.removeMemberFromGroup(groupId, userId);
}

export function getGroupMappings(groupId: string): Promise<GroupMappingsResponse> {
  return contract.getGroupMappings(groupId);
}

export function addGroupMapping(groupId: string, data: AddGroupMappingRequest): Promise<GroupMapping> {
  return contract.addGroupMapping(groupId, data);
}

export function removeGroupMapping(groupId: string, mappingId: string): Promise<void> {
  return contract.removeGroupMapping(groupId, mappingId);
}
