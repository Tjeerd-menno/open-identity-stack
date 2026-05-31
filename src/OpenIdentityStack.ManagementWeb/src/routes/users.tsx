import { UsersPage } from '@/features/users/UsersPage';
import { useAuth } from '@/lib/auth';

export function UsersRoute() {
  const { permissions } = useAuth();

  return <UsersPage permissions={permissions} />;
}
