import { useState } from 'react';
import { useParams, Navigate } from 'react-router-dom';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { RoleDetail } from '../components/RoleDetail';
import { RoleForm } from '../components/RoleForm';
import { useRole } from '../hooks/useRole';
import { useUpdateRole } from '../hooks/useUpdateRole';
import type { CreateRoleRequest, UpdateRoleRequest } from '@/types';

/**
 * Role detail/edit page
 * 
 * Displays role details and allows editing in place
 */
export function RoleDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [isEditing, setIsEditing] = useState(false);
  
  const { data: role, isLoading, error } = useRole(id);
  const { mutate: updateRole, isPending } = useUpdateRole();

  if (isLoading) {
    return (
      <div className="flex justify-center items-center min-h-[400px]">
        <LoadingSpinner />
      </div>
    );
  }

  if (error || !role) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Error Loading Role</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-destructive">
            {error instanceof Error ? error.message : 'Role not found'}
          </p>
        </CardContent>
      </Card>
    );
  }

  const handleEdit = () => {
    setIsEditing(true);
  };

  const handleSubmit = (data: CreateRoleRequest) => {
    if (!id) return;
    
    // Convert to UpdateRoleRequest by omitting the name field
    const updateData: UpdateRoleRequest = {
      displayName: data.displayName,
      description: data.description,
      permissions: data.permissions,
      acknowledgeWildcardGrant: data.acknowledgeWildcardGrant,
    };
    
    updateRole(
      { id, data: updateData },
      {
        onSuccess: () => {
          setIsEditing(false);
        },
      }
    );
  };

  const handleCancel = () => {
    setIsEditing(false);
  };

  if (!id) {
    return <Navigate to="/roles" replace />;
  }

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      {isEditing ? (
        <>
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Edit Role</h1>
            <p className="text-muted-foreground">
              Update role information and permissions
            </p>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Role Details</CardTitle>
              <CardDescription>
                Modify the role information and permissions
              </CardDescription>
            </CardHeader>
            <CardContent>
              <RoleForm
                role={role}
                onSubmit={handleSubmit}
                onCancel={handleCancel}
                isLoading={isPending}
              />
            </CardContent>
          </Card>
        </>
      ) : (
        <RoleDetail role={role} onEdit={handleEdit} />
      )}
    </div>
  );
}
