/**
 * EditGroupPage Component
 * 
 * Page for editing an existing group
 */

import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { GroupForm } from '../components/GroupForm';
import { useGroup } from '../hooks/useGroup';
import { useUpdateGroup } from '../hooks/useUpdateGroup';
import { ArrowLeft } from 'lucide-react';
import type { UpdateGroupRequest } from '@/types';

export function EditGroupPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: group, isLoading } = useGroup(id!);
  const updateGroup = useUpdateGroup();

  const handleSubmit = async (data: UpdateGroupRequest) => {
    try {
      await updateGroup.mutateAsync({ id: id!, data });
      navigate(`/groups/${id}`);
    } catch (error) {
      console.error('Failed to update group:', error);
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

  if (!group) {
    return (
      <div className="space-y-4">
        <h1 className="text-3xl font-bold">Group Not Found</h1>
        <Button onClick={() => navigate('/groups')}>
          <ArrowLeft className="h-4 w-4 mr-2" />
          Back to Groups
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
          onClick={() => navigate(`/groups/${id}`)}
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold">Edit Group</h1>
          <p className="text-muted-foreground">{group.name}</p>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Group Details</CardTitle>
          <CardDescription>
            Update group information
          </CardDescription>
        </CardHeader>
        <CardContent>
          <GroupForm
            group={group}
            onSubmit={handleSubmit}
            onCancel={() => navigate(`/groups/${id}`)}
            isSubmitting={updateGroup.isPending}
          />
        </CardContent>
      </Card>
    </div>
  );
}
