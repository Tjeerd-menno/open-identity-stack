import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, Edit, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { RoleTypeBadge } from './RoleTypeBadge';
import { useDeleteRole } from '../hooks/useDeleteRole';
import type { Role } from '@/types';

interface RoleDetailProps {
  /**
   * Role to display
   */
  role: Role;
  
  /**
   * Callback when edit button is clicked
   */
  onEdit: () => void;
}

/**
 * Role detail view component
 * 
 * Displays full role information including permissions, metadata,
 * and provides actions for editing and deleting (if allowed).
 */
export function RoleDetail({ role, onEdit }: RoleDetailProps) {
  const navigate = useNavigate();
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const { mutate: deleteRole, isPending: isDeleting } = useDeleteRole();

  const handleDelete = () => {
    deleteRole(role.id, {
      onSuccess: () => {
        navigate('/roles');
      },
      onError: (error) => {
        console.error('Failed to delete role:', error);
      },
    });
  };

  // Group permissions by resource for better display
  const groupedPermissions = role.permissions.reduce((acc, permission) => {
    const [resource] = permission.split(':');
    if (!acc[resource]) {
      acc[resource] = [];
    }
    acc[resource].push(permission);
    return acc;
  }, {} as Record<string, string[]>);

  return (
    <div className="space-y-6">
      {/* Header with actions */}
      <div className="flex items-center justify-between">
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <h2 className="text-2xl font-bold tracking-tight">{role.displayName}</h2>
            <RoleTypeBadge role={role} />
            {!role.isActive && (
              <Badge variant="destructive">Inactive</Badge>
            )}
          </div>
          <p className="text-muted-foreground">{role.name}</p>
        </div>
        
        <div className="flex gap-2">
          <Button onClick={onEdit} variant="outline">
            <Edit className="mr-2 h-4 w-4" />
            Edit
          </Button>
          
          {!role.isSystemRole && (
            <Button
              onClick={() => setIsDeleteDialogOpen(true)}
              variant="destructive"
              disabled={isDeleting}
            >
              <Trash2 className="mr-2 h-4 w-4" />
              Delete
            </Button>
          )}
        </div>
      </div>

      {/* System role warning */}
      {role.isSystemRole && (
        <Alert>
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>System Role</AlertTitle>
          <AlertDescription>
            This is a system role and cannot be deleted. Permissions can be modified but
            the role will always exist in the system.
          </AlertDescription>
        </Alert>
      )}

      {/* Description card */}
      {role.description && (
        <Card>
          <CardHeader>
            <CardTitle>Description</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">{role.description}</p>
          </CardContent>
        </Card>
      )}

      {/* Permissions card */}
      <Card>
        <CardHeader>
          <CardTitle>Permissions</CardTitle>
          <CardDescription>
            This role grants the following permissions to assigned users
          </CardDescription>
        </CardHeader>
        <CardContent>
          {role.permissions.length === 0 ? (
            <p className="text-sm text-muted-foreground">No permissions assigned</p>
          ) : (
            <div className="space-y-4">
              {Object.entries(groupedPermissions).map(([resource, permissions]) => (
                <div key={resource}>
                  <h4 className="text-sm font-semibold capitalize mb-2">
                    {resource}
                  </h4>
                  <div className="flex flex-wrap gap-2">
                    {permissions.map((permission) => (
                      <Badge key={permission} variant="secondary">
                        {permission}
                      </Badge>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Metadata card */}
      <Card>
        <CardHeader>
          <CardTitle>Metadata</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">Role ID:</span>
            <span className="font-mono text-xs">{role.id}</span>
          </div>
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">Status:</span>
            <span>{role.isActive ? 'Active' : 'Inactive'}</span>
          </div>
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">Type:</span>
            <span>{role.isSystemRole ? 'System' : 'Custom'}</span>
          </div>
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">Permission Count:</span>
            <span>{role.permissions.length}</span>
          </div>
        </CardContent>
      </Card>

      {/* Delete confirmation dialog */}
      <ConfirmDialog
        open={isDeleteDialogOpen}
        onOpenChange={setIsDeleteDialogOpen}
        onConfirm={handleDelete}
        title="Delete Role"
        description={`Are you sure you want to delete the role "${role.displayName}"? This action cannot be undone.`}
        confirmLabel="Delete"
        cancelLabel="Cancel"
        variant="destructive"
      />
    </div>
  );
}
