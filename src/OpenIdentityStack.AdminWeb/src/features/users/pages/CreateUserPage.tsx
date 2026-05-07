/**
 * CreateUserPage Component
 * 
 * Page for creating a new user
 */

import { useNavigate } from 'react-router-dom';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { UserForm } from '../components/UserForm';
import { useCreateUser } from '../hooks/useCreateUser';
import { ArrowLeft } from 'lucide-react';
import type { CreateUserRequest } from '@/types';

export function CreateUserPage() {
  const navigate = useNavigate();
  const createUser = useCreateUser();

  const handleSubmit = async (data: CreateUserRequest) => {
    try {
      const newUser = await createUser.mutateAsync(data);
      navigate(`/users/${newUser.id}`);
    } catch (error) {
      console.error('Failed to create user:', error);
      throw error;
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => navigate('/users')}
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <h1 className="text-3xl font-bold">Create User</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>User Details</CardTitle>
          <CardDescription>
            Create a new user account
          </CardDescription>
        </CardHeader>
        <CardContent>
          <UserForm
            onSubmit={handleSubmit}
            onCancel={() => navigate('/users')}
            isSubmitting={createUser.isPending}
          />
        </CardContent>
      </Card>
    </div>
  );
}
