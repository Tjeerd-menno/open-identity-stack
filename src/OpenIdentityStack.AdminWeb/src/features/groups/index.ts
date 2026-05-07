/**
 * Groups feature barrel export
 */

// Pages
export { GroupsPage } from './pages/GroupsPage';
export { GroupDetailPage } from './pages/GroupDetailPage';
export { CreateGroupPage } from './pages/CreateGroupPage';
export { EditGroupPage } from './pages/EditGroupPage';

// Components
export {
  GroupList,
  GroupDetail,
  GroupForm,
  GroupMembersList,
  GroupMappingsList,
  AddMemberDialog,
  AddMappingDialog,
} from './components';

// Hooks
export { useGroups } from './hooks/useGroups';
export { useGroup } from './hooks/useGroup';
export { useCreateGroup } from './hooks/useCreateGroup';
export { useUpdateGroup } from './hooks/useUpdateGroup';
export { useDeleteGroup } from './hooks/useDeleteGroup';
export { useGroupMembers } from './hooks/useGroupMembers';
export { useAddMember } from './hooks/useAddMember';
export { useRemoveMember } from './hooks/useRemoveMember';
export { useGroupMappings } from './hooks/useGroupMappings';
export { useAddMapping } from './hooks/useAddMapping';
export { useRemoveMapping } from './hooks/useRemoveMapping';

// API
export * from './api/groups-api';
