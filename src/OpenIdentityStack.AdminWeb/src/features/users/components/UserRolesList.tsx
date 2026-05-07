/**
 * UserRolesList Component
 * 
 * Displays list of roles assigned to a user with assign/unassign actions
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useUserRoles } from '../hooks/useUserRoles';
import { useUnassignRole } from '../hooks/useUnassignRole';
import { AssignRoleDialog } from './AssignRoleDialog';
import { X, Plus } from 'lucide-react';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';

interface UserRolesListProps {
  userId: string;
}

export function UserRolesList({ userId }: UserRolesListProps) {
  const { data: roles, isLoading } = useUserRoles(userId);
  const unassignRole = useUnassignRole();
  const [_showAssignDialog, _setShowAssignDialog] = useState(false);

  const handleUnassign = async (roleId: string) => {
    try {
      await unassignRole.mutateAsync({ userId, roleId });
    } catch (error) {
      console.error('Failed to unassign role:', error);
    }
  };

  if (isLoading) {
    return <LoadingSpinner />;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Roles</h3>
        <Button 
          onClick={() => _setShowAssignDialog(true)}
          size="sm"
        >
          <Plus className="h-4 w-4 mr-1" />
          Assign Role
        </Button>
      </div>

      {!roles || roles.length === 0 ? (
        <p className="text-sm text-muted-foreground">No roles assigned</p>
      ) : (
        <div className="space-y-2">
          {roles.map((role) => (
            <div
              key={role.id}
              className="flex items-center justify-between p-2 border rounded-md"
              data-testid="role-item"
            >
              <div>
                <div className="font-medium">{role.displayName}</div>
                <div className="text-sm text-muted-foreground">{role.name}</div>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => handleUnassign(role.id)}
                aria-label={`Remove role ${role.displayName}`}
                disabled={role.isSystemRole}
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          ))}
        </div>
      )}

      <AssignRoleDialog 
        userId={userId} 
        open={_showAssignDialog} 
        onOpenChange={_setShowAssignDialog} 
      />
    </div>
  );
}
