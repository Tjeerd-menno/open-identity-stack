import { useNavigate } from 'react-router-dom';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { RoleForm } from '../components/RoleForm';
import { useCreateRole } from '../hooks/useCreateRole';
import type { CreateRoleRequest } from '@/types';

/**
 * Create role page
 * 
 * Provides a form to create a new role with permissions
 */
export function CreateRolePage() {
  const navigate = useNavigate();
  const { mutate: createRole, isPending } = useCreateRole();

  const handleSubmit = (data: CreateRoleRequest) => {
    createRole(data, {
      onSuccess: (role) => {
        navigate(`/roles/${role.id}`);
      },
    });
  };

  const handleCancel = () => {
    navigate('/roles');
  };

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Create Role</h1>
        <p className="text-muted-foreground">
          Create a new role with custom permissions
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Role Details</CardTitle>
          <CardDescription>
            Enter the role information and select permissions
          </CardDescription>
        </CardHeader>
        <CardContent>
          <RoleForm
            onSubmit={handleSubmit}
            onCancel={handleCancel}
            isLoading={isPending}
          />
        </CardContent>
      </Card>
    </div>
  );
}
