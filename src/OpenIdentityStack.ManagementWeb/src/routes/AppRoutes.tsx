import { Navigate, Route, Routes } from 'react-router';
import { AppShell } from '@/components/AppShell';
import { AuthCallbackRoute } from './auth-callback';
import { PlaceholderPage } from './placeholder';
import { UsersRoute } from './users';

export function AppRoutes() {
  return (
    <Routes>
      <Route path="auth/callback" element={<AuthCallbackRoute />} />
      <Route element={<AppShell />}>
        <Route index element={<Navigate to="/users" replace />} />
        <Route path="users" element={<UsersRoute />} />
        <Route path="roles" element={<PlaceholderPage title="Roles" />} />
        <Route path="groups" element={<PlaceholderPage title="Groups" />} />
        <Route path="service-accounts" element={<PlaceholderPage title="Service accounts" />} />
        <Route path="identity-providers" element={<PlaceholderPage title="Identity providers" />} />
        <Route path="audit" element={<PlaceholderPage title="Audit" />} />
      </Route>
    </Routes>
  );
}
