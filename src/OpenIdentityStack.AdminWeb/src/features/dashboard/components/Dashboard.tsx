import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useUsers } from '@/features/users';
import { useRoles } from '@/features/roles';
import { useGroups } from '@/features/groups';
import { useSessions } from '@/features/sessions';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { Users, Shield, UserPlus, Activity } from 'lucide-react';

export function Dashboard() {
  const { isAuthenticated, isLoading } = useAuth();
  
  // Debug logging
  console.log('[Dashboard] isAuthenticated:', isAuthenticated, 'isLoading:', isLoading);
  
  // Only fetch data when authenticated to prevent 401 errors during auth flow
  const { data: usersData } = useUsers({ enabled: isAuthenticated });
  const { data: rolesData } = useRoles({ enabled: isAuthenticated });
  const { data: groupsData } = useGroups({ enabled: isAuthenticated });
  const { data: sessionsData } = useSessions({ enabled: isAuthenticated });

  const stats = [
    {
      title: 'Total Users',
      value: usersData?.totalCount ?? 0,
      description: 'Registered user accounts',
      icon: Users,
    },
    {
      title: 'Roles',
      value: rolesData?.totalCount ?? 0,
      description: 'Permission groups',
      icon: Shield,
    },
    {
      title: 'Groups',
      value: groupsData?.totalCount ?? 0,
      description: 'User groups',
      icon: UserPlus,
    },
    {
      title: 'Active Sessions',
      value: sessionsData?.items?.filter((s) => s.status === 'Active').length ?? 0,
      description: 'Current user sessions',
      icon: Activity,
    },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Dashboard</h1>
        <p className="text-muted-foreground">
          Overview of your OpenIdentityStack IAM system
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {stats.map((stat) => {
          const Icon = stat.icon;
          return (
            <Card key={stat.title}>
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                <CardTitle className="text-sm font-medium">{stat.title}</CardTitle>
                <Icon className="h-4 w-4 text-muted-foreground" />
              </CardHeader>
              <CardContent>
                <div className="text-2xl font-bold">{stat.value}</div>
                <p className="text-xs text-muted-foreground">{stat.description}</p>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Welcome</CardTitle>
            <CardDescription>OpenIdentityStack Admin Portal</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">
              Manage users, roles, groups, service accounts, sessions, and identity providers
              for your OpenIdentityStack IAM system.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Quick Actions</CardTitle>
            <CardDescription>Common administrative tasks</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            <div className="text-sm">
              <a href="/users/create" className="text-primary hover:underline">
                • Create new user
              </a>
            </div>
            <div className="text-sm">
              <a href="/roles/new" className="text-primary hover:underline">
                • Create new role
              </a>
            </div>
            <div className="text-sm">
              <a href="/groups/new" className="text-primary hover:underline">
                • Create new group
              </a>
            </div>
            <div className="text-sm">
              <a href="/sessions" className="text-primary hover:underline">
                • View active sessions
              </a>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
