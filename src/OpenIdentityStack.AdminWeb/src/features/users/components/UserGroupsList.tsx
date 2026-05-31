/**
 * UserGroupsList Component
 * 
 * Displays list of groups a user belongs to.
 * Requires 'groups:read' permission to view groups.
 */

import { useUserGroups } from '../hooks/useUserGroups';
import { usePermission } from '@/features/auth/hooks/useAuth';
import { Badge } from '@/components/ui/badge';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';

interface UserGroupsListProps {
  userId: string;
}

export function UserGroupsList({ userId }: UserGroupsListProps) {
  const hasGroupsRead = usePermission('groups:read');
  const { data: groups, isLoading } = useUserGroups(userId);

  if (!hasGroupsRead) {
    return (
      <div className="space-y-4">
        <h3 className="text-lg font-semibold">Groups</h3>
        <p className="text-sm text-muted-foreground">
          You do not have permission to view groups for this user.
        </p>
      </div>
    );
  }

  if (isLoading) {
    return <LoadingSpinner />;
  }

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">Groups</h3>

      {!groups || groups.length === 0 ? (
        <p className="text-sm text-muted-foreground">No groups</p>
      ) : (
        <div className="space-y-2">
          {groups.map((group) => (
            <div
              key={group.id}
              className="flex items-center justify-between p-2 border rounded-md"
              data-testid="group-item"
            >
              <div>
                <div className="font-medium">{group.name}</div>
                {group.description && (
                  <div className="text-sm text-muted-foreground">
                    {group.description}
                  </div>
                )}
              </div>
              <Badge variant="secondary">{group.memberCount} members</Badge>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
