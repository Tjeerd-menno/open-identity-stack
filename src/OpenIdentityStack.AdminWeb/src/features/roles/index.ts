/**
 * Roles Feature Module
 * 
 * Exports all role management components, hooks, and pages
 */

// Pages
export { RolesPage } from './pages/RolesPage';
export { CreateRolePage } from './pages/CreateRolePage';
export { RoleDetailPage } from './pages/RoleDetailPage';

// Components
export { RoleList } from './components/RoleList';
export { RoleDetail } from './components/RoleDetail';
export { RoleForm } from './components/RoleForm';
export { RoleTypeBadge } from './components/RoleTypeBadge';
export { PermissionSelector } from './components/PermissionSelector';

// Hooks
export { useRoles } from './hooks/useRoles';
export { useRole } from './hooks/useRole';
export { useCreateRole } from './hooks/useCreateRole';
export { useUpdateRole } from './hooks/useUpdateRole';
export { useDeleteRole } from './hooks/useDeleteRole';

// API
export * from './api/roles-api';
