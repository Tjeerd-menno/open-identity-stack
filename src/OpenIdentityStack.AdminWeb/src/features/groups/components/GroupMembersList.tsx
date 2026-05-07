/**
 * GroupMembersList Component
 * 
 * Displays list of members in a group with add/remove actions
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useGroupMembers } from '../hooks/useGroupMembers';
import { useRemoveMember } from '../hooks/useRemoveMember';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { AddMemberDialog } from './AddMemberDialog';
import { X, Plus } from 'lucide-react';

interface GroupMembersListProps {
  groupId: string;
}

export function GroupMembersList({ groupId }: GroupMembersListProps) {
  const { data, isLoading } = useGroupMembers(groupId);
  const removeMember = useRemoveMember();
  const [showAddDialog, setShowAddDialog] = useState(false);
  const [memberToRemove, setMemberToRemove] = useState<string | null>(null);

  const handleRemove = async (userId: string) => {
    try {
      await removeMember.mutateAsync({ groupId, userId });
      setMemberToRemove(null);
    } catch (error) {
      console.error('Failed to remove member:', error);
    }
  };

  if (isLoading) {
    return <LoadingSpinner />;
  }

  const members = data?.items || [];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Members ({members.length})</h3>
        <Button 
          onClick={() => setShowAddDialog(true)}
          size="sm"
        >
          <Plus className="h-4 w-4 mr-1" />
          Add Member
        </Button>
      </div>

      {members.length === 0 ? (
        <p className="text-sm text-muted-foreground">No members in this group</p>
      ) : (
        <div className="space-y-2">
          {members.map((member) => (
            <div
              key={member.userId}
              className="flex items-center justify-between p-3 border rounded-md hover:bg-muted/50 transition-colors"
              data-testid="member-item"
            >
              <div className="flex-1">
                <div className="font-medium">{member.displayName}</div>
                <div className="text-sm text-muted-foreground">{member.email}</div>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setMemberToRemove(member.userId)}
                aria-label={`Remove member ${member.displayName}`}
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          ))}
        </div>
      )}

      {/* Add Member Dialog */}
      <AddMemberDialog
        groupId={groupId}
        open={showAddDialog}
        onOpenChange={setShowAddDialog}
      />

      {/* Remove Member Confirmation Dialog */}
      <ConfirmDialog
        open={!!memberToRemove}
        onOpenChange={(open) => !open && setMemberToRemove(null)}
        title="Remove Member"
        description="Are you sure you want to remove this member from the group?"
        onConfirm={() => memberToRemove && handleRemove(memberToRemove)}
        confirmLabel="Remove"
        variant="destructive"
        loading={removeMember.isPending}
      />
    </div>
  );
}
