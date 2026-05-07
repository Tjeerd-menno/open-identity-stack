/**
 * GroupDetail Component
 * 
 * Displays detailed information about a group including members and mappings
 */

import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useGroup } from '../hooks/useGroup';
import { useDeleteGroup } from '../hooks/useDeleteGroup';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { GroupMembersList } from './GroupMembersList';
import { GroupMappingsList } from './GroupMappingsList';
import { ArrowLeft, Edit, Trash2 } from 'lucide-react';

export function GroupDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: group, isLoading } = useGroup(id!);
  const deleteGroup = useDeleteGroup();
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

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

  const handleDelete = async () => {
    try {
      await deleteGroup.mutateAsync(id!);
      navigate('/groups');
    } catch (error) {
      console.error('Failed to delete group:', error);
    }
  };

  const formatDate = (dateString: string | null) => {
    if (!dateString) return 'N/A';
    return new Date(dateString).toLocaleString();
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate('/groups')}
          >
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{group.name}</h1>
            {group.description && (
              <p className="text-muted-foreground">{group.description}</p>
            )}
          </div>
        </div>
        <div className="flex gap-2">
          <Button onClick={() => navigate(`/groups/${id}/edit`)} variant="outline">
            <Edit className="h-4 w-4 mr-2" />
            Edit
          </Button>
          <Button 
            onClick={() => setShowDeleteConfirm(true)}
            variant="destructive"
          >
            <Trash2 className="h-4 w-4 mr-2" />
            Delete
          </Button>
        </div>
      </div>

      {/* Group Information Card */}
      <Card>
        <CardHeader>
          <CardTitle>Group Information</CardTitle>
          <CardDescription>Basic group details</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <div className="text-sm font-medium text-muted-foreground">Group ID</div>
              <div className="mt-1 text-xs font-mono">{group.id}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Members</div>
              <div className="mt-1">{group.memberCount}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Mappings</div>
              <div className="mt-1">{group.mappingCount}</div>
            </div>
            <div>
              <div className="text-sm font-medium text-muted-foreground">Created</div>
              <div className="mt-1">{formatDate(group.createdAt)}</div>
            </div>
            {group.modifiedAt && (
              <div>
                <div className="text-sm font-medium text-muted-foreground">Modified</div>
                <div className="mt-1">{formatDate(group.modifiedAt)}</div>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Members Card */}
      <Card>
        <CardHeader>
          <CardTitle>Members</CardTitle>
          <CardDescription>Users in this group</CardDescription>
        </CardHeader>
        <CardContent>
          <GroupMembersList groupId={id!} />
        </CardContent>
      </Card>

      {/* Mappings Card */}
      <Card>
        <CardHeader>
          <CardTitle>Mappings</CardTitle>
          <CardDescription>Role and claim mappings for this group</CardDescription>
        </CardHeader>
        <CardContent>
          <GroupMappingsList groupId={id!} />
        </CardContent>
      </Card>

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        open={showDeleteConfirm}
        onOpenChange={(open) => !open && setShowDeleteConfirm(false)}
        title="Delete Group"
        description="Are you sure you want to permanently delete this group? This action cannot be undone and will remove all members and mappings."
        onConfirm={handleDelete}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteGroup.isPending}
      />
    </div>
  );
}
