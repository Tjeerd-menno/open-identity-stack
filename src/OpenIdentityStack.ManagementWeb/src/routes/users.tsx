import { useEffect } from 'react';
import { UsersPage } from '@/features/users/UsersPage';
import { useAuth } from '@/lib/auth';

export function UsersRoute() {
  const { isAuthenticated, isLoading, permissions, login } = useAuth();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      login();
    }
  }, [isAuthenticated, isLoading, login]);

  if (isLoading) {
    return <div>Loading...</div>;
  }

  if (!isAuthenticated) {
    return null;
  }

  return <UsersPage permissions={permissions} />;
}
