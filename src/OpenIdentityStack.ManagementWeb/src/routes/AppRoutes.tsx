import { type ReactElement } from 'react';
import { Navigate, Route, Routes } from 'react-router';
import { AppShell } from '@/components/AppShell';
import { ApplicationsPage } from '@/features/applications/ApplicationsPage';
import { ApplicationDetailPage } from '@/features/applications/ApplicationDetailPage';
import { CreateApplicationPage } from '@/features/applications/CreateApplicationPage';
import { EditApplicationPage } from '@/features/applications/EditApplicationPage';
import { ApplicationPermissionsPage } from '@/features/application-permissions/ApplicationPermissionsPage';
import { GroupsPage } from '@/features/groups/GroupsPage';
import { ProvidersPage } from '@/features/providers/ProvidersPage';
import { RolesPage } from '@/features/roles/RolesPage';
import { SessionsPage } from '@/features/sessions/SessionsPage';
import { SettingsPage } from '@/features/settings/SettingsPage';
import { AuthCallbackRoute } from './auth-callback';
import { PlaceholderPage } from './placeholder';
import { AuthenticatedRoute, managementRoutePermissions, RequirePermission } from './route-guards';
import { UsersRoute } from './users';

export function AppRoutes() {
  return (
    <Routes>
      <Route path="auth/callback" element={<AuthCallbackRoute />} />
      <Route element={<AuthenticatedRoute><AppShell /></AuthenticatedRoute>}>
        <Route index element={<Navigate to="/users" replace />} />
        <Route path="users" element={withPermission(<UsersRoute />, managementRoutePermissions.users)} />
        <Route path="users/create" element={withPermission(<UsersRoute />, ['users:write'])} />
        <Route path="users/:id" element={withPermission(<UsersRoute />, managementRoutePermissions.users)} />
        <Route path="users/:id/edit" element={withPermission(<UsersRoute />, ['users:write'])} />
        <Route path="roles" element={withPermission(<RolesPage />, managementRoutePermissions.roles)} />
        <Route path="roles/new" element={withPermission(<RolesPage />, ['roles:write'])} />
        <Route path="roles/:id" element={withPermission(<RolesPage />, managementRoutePermissions.roles)} />
        <Route path="groups" element={withPermission(<GroupsPage />, managementRoutePermissions.groups)} />
        <Route path="groups/new" element={withPermission(<GroupsPage />, ['groups:write'])} />
        <Route path="groups/:id" element={withPermission(<GroupsPage />, managementRoutePermissions.groups)} />
        <Route path="groups/:id/edit" element={withPermission(<GroupsPage />, ['groups:write'])} />
        <Route path="applications" element={withPermission(<ApplicationsPage />, managementRoutePermissions.applications)} />
        <Route path="applications/new" element={withPermission(<CreateApplicationPage />, ['applications:write'])} />
        <Route path="applications/:id" element={withPermission(<ApplicationDetailPage />, managementRoutePermissions.applications)} />
        <Route path="applications/:id/edit" element={withPermission(<EditApplicationPage />, ['applications:write'])} />
        <Route path="application-permissions" element={withPermission(<ApplicationPermissionsPage />, managementRoutePermissions.applicationPermissions)} />
        <Route path="application-permissions/new" element={withPermission(<ApplicationPermissionsPage />, ['application-permissions:write'])} />
        <Route path="application-permissions/:id" element={withPermission(<ApplicationPermissionsPage />, managementRoutePermissions.applicationPermissions)} />
        <Route path="sessions" element={withPermission(<SessionsPage />, managementRoutePermissions.sessions)} />
        <Route path="sessions/:id" element={withPermission(<SessionsPage />, managementRoutePermissions.sessions)} />
        <Route path="providers" element={withPermission(<ProvidersPage />, managementRoutePermissions.providers)} />
        <Route path="providers/new" element={withPermission(<ProvidersPage />, ['providers:write'])} />
        <Route path="providers/:id" element={withPermission(<ProvidersPage />, managementRoutePermissions.providers)} />
        <Route path="providers/:id/edit" element={withPermission(<ProvidersPage />, ['providers:write'])} />
        <Route path="settings" element={withPermission(<SettingsPage />, managementRoutePermissions.settings)} />
        <Route path="audit-entries" element={withPermission(<PlaceholderPage title="Audit" />, managementRoutePermissions.audit)} />
      </Route>
    </Routes>
  );
}

function withPermission(element: ReactElement, requiredPermissions: readonly string[]) {
  return <RequirePermission requiredPermissions={requiredPermissions}>{element}</RequirePermission>;
}
