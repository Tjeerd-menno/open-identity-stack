/**
 * Users Feature Index
 * 
 * Exports all user management components and pages
 */

// Pages
export { CreateUserPage } from './pages/CreateUserPage';
export { EditUserPage } from './pages/EditUserPage';

// Components
export { UserList } from './components/UserList';
export { UserDetail } from './components/UserDetail';
export { UserForm } from './components/UserForm';
export { UserStatusBadge } from './components/UserStatusBadge';
export { UserRolesList } from './components/UserRolesList';
export { UserGroupsList } from './components/UserGroupsList';
export { UpstreamIdentitiesList } from './components/UpstreamIdentitiesList';
export { ResetPasswordDialog } from './components/ResetPasswordDialog';

// Hooks
export { useUsers } from './hooks/useUsers';
export { useUser } from './hooks/useUser';
export { useCreateUser } from './hooks/useCreateUser';
export { useUpdateUser } from './hooks/useUpdateUser';
export { useDeleteUser } from './hooks/useDeleteUser';
export { useDisableUser } from './hooks/useDisableUser';
export { useEnableUser } from './hooks/useEnableUser';
export { useResetPassword } from './hooks/useResetPassword';
export { useUserRoles } from './hooks/useUserRoles';
export { useAssignRole } from './hooks/useAssignRole';
export { useUnassignRole } from './hooks/useUnassignRole';
export { useUserGroups } from './hooks/useUserGroups';
export { useUpstreamIdentities } from './hooks/useUpstreamIdentities';
export { useLinkIdentity } from './hooks/useLinkIdentity';
export { useUnlinkIdentity } from './hooks/useUnlinkIdentity';

// API
export * from './api/users-api';
