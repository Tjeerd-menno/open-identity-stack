import { usersWrite } from '@/lib/generated-permissions';

export const wrongUsersListRoute = (
  <Route
    path="users"
    element={
      <RequirePermissions permissions={[usersWrite]}>
        <UsersPage />
      </RequirePermissions>
    }
  />
);
