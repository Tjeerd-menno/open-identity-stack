/**
 * EditUserPage Component
 * 
 * Page for editing an existing user
 */

import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { UserForm } from '../components/UserForm';
import { useUser } from '../hooks/useUser';
import { useUpdateUser } from '../hooks/useUpdateUser';
import { ArrowLeft } from 'lucide-react';
import type { UpdateUserRequest } from '@/types';

export function EditUserPage() {
  const { userId } = useParams<{ userId: string }>();
  const navigate = useNavigate();
  const { data: user, isLoading } = useUser(userId!);
  const updateUser = useUpdateUser();

  const handleSubmit = async (data: UpdateUserRequest) => {
    try {
      await updateUser.mutateAsync({ userId: userId!, data });
      navigate(`/users/${userId}`);
    } catch (error) {
      console.error('Failed to update user:', error);
      throw error;
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <LoadingSpinner />
      </div>
    );
  }

  if (!user) {
    return (
      <div className="space-y-4">
        <h1 className="text-3xl font-bold">User Not Found</h1>
        <Button onClick={() => navigate('/users')}>
          <ArrowLeft className="h-4 w-4 mr-2" />
          Back to Users
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => navigate(`/users/${userId}`)}
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold">Edit User</h1>
          <p className="text-muted-foreground">{user.email}</p>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>User Details</CardTitle>
          <CardDescription>
            Update user information
          </CardDescription>
        </CardHeader>
        <CardContent>
          <UserForm
            user={user}
            onSubmit={handleSubmit}
            onCancel={() => navigate(`/users/${userId}`)}
            isSubmitting={updateUser.isPending}
          />
        </CardContent>
      </Card>
    </div>
  );
}
