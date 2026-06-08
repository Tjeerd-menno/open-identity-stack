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
import { adminApiClient } from '@/lib/api/client';
import { createGroupsContract, type GroupListParams } from '@openidentitystack/admin-api-client';

const contract = createGroupsContract(adminApiClient);

export async function getGroups(page = 1, pageSize = 20, search?: string): Promise<PaginatedResponse<GroupListItem>> {
  return contract.getGroups({
    page,
    pageSize,
    ...(search ? { search } : {}),
  }) as Promise<PaginatedResponse<GroupListItem>>;
}

export async function getGroup(id: string): Promise<Group> {
  return contract.getGroup(id);
}

export async function createGroup(data: CreateGroupRequest): Promise<Group> {
  return contract.createGroup(data);
}

export async function updateGroup(id: string, data: UpdateGroupRequest): Promise<Group> {
  return contract.updateGroup(id, data) as unknown as Promise<Group>;
}

export async function deleteGroup(id: string): Promise<void> {
  await contract.deleteGroup(id);
}

export async function getGroupMembers(
  groupId: string,
  page = 1,
  pageSize = 20
): Promise<PaginatedResponse<GroupMember>> {
  return contract.getGroupMembers(groupId, { page, pageSize }) as Promise<PaginatedResponse<GroupMember>>;
}

export async function addMemberToGroup(groupId: string, userId: string): Promise<void> {
  await contract.addMemberToGroup(groupId, userId);
}

export async function removeMemberFromGroup(groupId: string, userId: string): Promise<void> {
  await contract.removeMemberFromGroup(groupId, userId);
}

export async function getGroupMappings(groupId: string): Promise<{ items: GroupMapping[] }> {
  return contract.getGroupMappings(groupId) as Promise<{ items: GroupMapping[] }>;
}

export async function addGroupMapping(groupId: string, data: AddGroupMappingRequest): Promise<GroupMapping> {
  return contract.addGroupMapping(groupId, data) as Promise<GroupMapping>;
}

export async function removeGroupMapping(groupId: string, mappingId: string): Promise<void> {
  await contract.removeGroupMapping(groupId, mappingId);
}
