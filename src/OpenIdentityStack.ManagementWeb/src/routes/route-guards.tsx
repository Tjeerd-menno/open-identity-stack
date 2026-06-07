import { Alert, Stack, Text, Title } from '@mantine/core';
import { useEffect, type ReactNode } from 'react';
import { LoadingState } from '@/components/PagePrimitives';
import { useAuth } from '@/lib/auth-context';
import { hasAnyPermission } from '@/lib/permissions';

export const managementRoutePermissions = {
  users: ['users:read'],
  roles: ['roles:read'],
  groups: ['groups:read'],
  applications: ['applications:read'],
  applicationPermissions: ['application-permissions:read'],
  sessions: ['sessions:read'],
  providers: ['providers:read'],
  settings: ['system:settings'],
  audit: ['audit-logs:read'],
} as const;

export function AuthenticatedRoute({ children }: { children: ReactNode }) {
  const { isAuthenticated, isLoading, login } = useAuth();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      void login();
    }
  }, [isAuthenticated, isLoading, login]);

  if (isLoading) {
    return (
      <LoadingState
        label="Loading management session"
        title="Opening management console"
        description="Checking the current operator session before loading the IAM workspace."
      />
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  return children;
}

export function RequirePermission({
  children,
  requiredPermissions,
}: {
  children: ReactNode;
  requiredPermissions: readonly string[];
}) {
  const { permissions } = useAuth();

  if (!hasAnyPermission(permissions, [...requiredPermissions])) {
    return (
      <Stack gap="sm">
        <Alert color="red">
          <Title order={1}>Access denied</Title>
          <Text>You do not have the required permission for this section.</Text>
          <Text size="sm">Required: {requiredPermissions.join(' or ')}</Text>
        </Alert>
      </Stack>
    );
  }

  return children;
}
