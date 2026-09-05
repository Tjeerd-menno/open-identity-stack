import { CutoverReadinessPage } from '@/features/security/CutoverReadinessPage';
import { Box, Button, Card, Center, Loader, Stack, Text, ThemeIcon, Title } from '@mantine/core';
import { type ReactNode } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router';
import { AppShell } from '@/components/AppShell';
import { Icon } from '@/components/Icon';
import { CenteredState } from '@/components/primitives';
import { useAuth } from '@/lib/auth-context';
import { hasAnyPermission } from '@/lib/permissions';
import { OverviewPage } from '@/features/overview/OverviewPage';
import { UsersPage } from '@/features/users/UsersPage';
import { UserDetailPage } from '@/features/users/UserDetailPage';
import { ApplicationsPage } from '@/features/applications/ApplicationsPage';
import { ApplicationDetailPage } from '@/features/applications/ApplicationDetailPage';
import { RolesPage } from '@/features/roles/RolesPage';
import { RoleDetailPage } from '@/features/roles/RoleDetailPage';
import { GroupsPage } from '@/features/groups/GroupsPage';
import { GroupDetailPage } from '@/features/groups/GroupDetailPage';
import { PermissionsPage } from '@/features/permissions/PermissionsPage';
import { PermissionsDetailPage } from '@/features/permissions/PermissionsDetailPage';
import { SessionsPage } from '@/features/sessions/SessionsPage';
import { SessionDetailPage } from '@/features/sessions/SessionDetailPage';
import { ProvidersPage } from '@/features/providers/ProvidersPage';
import { ProviderDetailPage } from '@/features/providers/ProviderDetailPage';
import { AuthenticationSettingsPage } from '@/features/providers/AuthenticationSettingsPage';
import { AuditPage } from '@/features/audit/AuditPage';

function BrandLockup({ size = 'lg' }: { size?: 'lg' | 'sm' }) {
  const dim = size === 'lg' ? 36 : 30;
  return (
    <ThemeIcon color="blue" radius="md" size={dim} variant="filled">
      <Icon name="shield-check" size={size === 'lg' ? 21 : 18} stroke={2.2} />
    </ThemeIcon>
  );
}

function LoginScreen() {
  const auth = useAuth();
  return (
    <Center mih="100vh" p="md" style={{ background: 'var(--mw-surface-sunken)' }}>
      <Box w={400} maw="100%">
        <Stack align="center" gap="xs" mb="lg">
          <BrandLockup />
          <Text fw={700} size="xl" style={{ letterSpacing: '-0.3px' }}>
            OpenIdentity
            <Text c="blue" component="span" fw={700} inherit>
              Stack
            </Text>
          </Text>
        </Stack>
        <Card padding="xl" shadow="sm">
          <Title order={2}>Sign in</Title>
          <Text c="dimmed" mt={4} mb="lg" size="sm">
            Management Web · operator console
          </Text>
          <Button fullWidth size="md" onClick={() => void auth.login()}>
            Sign in
          </Button>
        </Card>
        <Text c="dimmed" mt="lg" size="xs" ta="center">
          Protected by OpenIdentityStack · OpenID Connect
        </Text>
      </Box>
    </Center>
  );
}

function FullScreenLoader({ label }: { label: string }) {
  return (
    <Center mih="100vh" style={{ background: 'var(--mw-surface-sunken)' }}>
      <Stack align="center" gap="sm">
        <Loader color="blue" size="lg" />
        <Text c="dimmed" size="sm">
          {label}
        </Text>
      </Stack>
    </Center>
  );
}

function RequirePermissions({ permissions, children }: { permissions: string[]; children: ReactNode }) {
  const auth = useAuth();
  if (!hasAnyPermission(auth.permissions, permissions)) {
    return (
      <CenteredState
        icon="shield-off"
        iconColor="orange"
        title="Access denied"
        text={`You need one of: ${permissions.join(', ')} to view this area.`}
      />
    );
  }
  return <>{children}</>;
}

export function AppRoutes() {
  const auth = useAuth();
  const location = useLocation();

  if (location.pathname === '/auth/callback' || location.pathname === '/auth/silent-callback') {
    return <FullScreenLoader label="Completing sign-in…" />;
  }

  if (auth.isLoading) {
    return <FullScreenLoader label="Loading console…" />;
  }

  if (!auth.isAuthenticated) {
    return <LoginScreen />;
  }

  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<OverviewPage />} />
        <Route path="security/cutover" element={<RequirePermissions permissions={['sessions:revoke']}><CutoverReadinessPage /></RequirePermissions>} />
        <Route
          path="users"
          element={
            <RequirePermissions permissions={['users:read']}>
              <UsersPage />
            </RequirePermissions>
          }
        />
        <Route
          path="users/:userId"
          element={
            <RequirePermissions permissions={['users:read']}>
              <UserDetailPage />
            </RequirePermissions>
          }
        />
        <Route
          path="groups"
          element={
            <RequirePermissions permissions={['groups:read']}>
              <GroupsPage />
            </RequirePermissions>
          }
        />
        <Route
          path="groups/:groupId"
          element={
            <RequirePermissions permissions={['groups:read']}>
              <GroupDetailPage />
            </RequirePermissions>
          }
        />
        <Route
          path="roles"
          element={
            <RequirePermissions permissions={['roles:read']}>
              <RolesPage />
            </RequirePermissions>
          }
        />
        <Route
          path="roles/:roleId"
          element={
            <RequirePermissions permissions={['roles:read']}>
              <RoleDetailPage />
            </RequirePermissions>
          }
        />
        <Route
          path="application-permissions"
          element={
            <RequirePermissions permissions={['application-permissions:read']}>
              <PermissionsPage />
            </RequirePermissions>
          }
        />
        <Route
          path="application-permissions/:registrationId"
          element={
            <RequirePermissions permissions={['application-permissions:read']}>
              <PermissionsDetailPage />
            </RequirePermissions>
          }
        />
        <Route
          path="applications"
          element={
            <RequirePermissions permissions={['applications:read']}>
              <ApplicationsPage />
            </RequirePermissions>
          }
        />
        <Route
          path="applications/:applicationId"
          element={
            <RequirePermissions permissions={['applications:read']}>
              <ApplicationDetailPage />
            </RequirePermissions>
          }
        />
        <Route
          path="sessions"
          element={
            <RequirePermissions permissions={['sessions:read']}>
              <SessionsPage />
            </RequirePermissions>
          }
        />
        <Route
          path="sessions/:sessionId"
          element={
            <RequirePermissions permissions={['sessions:read']}>
              <SessionDetailPage />
            </RequirePermissions>
          }
        />
        <Route
          path="providers"
          element={
            <RequirePermissions permissions={['providers:read']}>
              <ProvidersPage />
            </RequirePermissions>
          }
        />
        <Route
          path="providers/settings"
          element={
            <RequirePermissions permissions={['system:settings']}>
              <AuthenticationSettingsPage />
            </RequirePermissions>
          }
        />
        <Route
          path="providers/:providerId"
          element={
            <RequirePermissions permissions={['providers:read']}>
              <ProviderDetailPage />
            </RequirePermissions>
          }
        />
        <Route
          path="audit-entries"
          element={
            <RequirePermissions permissions={['audit-logs:read']}>
              <AuditPage />
            </RequirePermissions>
          }
        />
        {/* Preserve the /settings entry point; authentication settings live under providers. */}
        <Route path="settings" element={<Navigate replace to="/providers/settings" />} />
        <Route path="*" element={<Navigate replace to="/" />} />
      </Route>
    </Routes>
  );
}
