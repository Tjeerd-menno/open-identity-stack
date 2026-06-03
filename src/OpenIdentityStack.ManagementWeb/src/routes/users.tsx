import { UsersPage } from '@/features/users/UsersPage';
import { useAuth } from '@/lib/auth-context';

export function UsersRoute() {
  const { permissions } = useAuth();

  return <UsersPage permissions={permissions} />;
}
