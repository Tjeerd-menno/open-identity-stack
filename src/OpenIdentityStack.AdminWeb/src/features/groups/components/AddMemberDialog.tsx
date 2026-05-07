/**
 * AddMemberDialog Component
 * 
 * Dialog for adding a member to a group
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useUsers } from '@/features/users/hooks/useUsers';
import { useAddMember } from '../hooks/useAddMember';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';

interface AddMemberDialogProps {
  groupId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function AddMemberDialog({ groupId, open, onOpenChange }: AddMemberDialogProps) {
  const [selectedUserId, setSelectedUserId] = useState<string>('');
  const { data: usersData, isLoading: isLoadingUsers } = useUsers({ 
    page: 1, 
    pageSize: 100,
    search: '' 
  });
  const addMember = useAddMember();

  const handleAdd = async () => {
    if (!selectedUserId) return;

    try {
      await addMember.mutateAsync({ groupId, userId: selectedUserId });
      setSelectedUserId('');
      onOpenChange(false);
    } catch (error) {
      console.error('Failed to add member:', error);
    }
  };

  const users = usersData?.items || [];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Member to Group</DialogTitle>
          <DialogDescription>
            Select a user to add to this group
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {isLoadingUsers ? (
            <div className="flex justify-center py-6">
              <LoadingSpinner />
            </div>
          ) : (
            <Select value={selectedUserId} onValueChange={setSelectedUserId}>
              <SelectTrigger>
                <SelectValue placeholder="Select a user..." />
              </SelectTrigger>
              <SelectContent>
                {users.length === 0 ? (
                  <SelectItem disabled value="">
                    No users available
                  </SelectItem>
                ) : (
                  users.map((user) => (
                    <SelectItem key={user.id} value={user.id}>
                      <div className="flex flex-col">
                        <span>{user.displayName}</span>
                        <span className="text-xs text-muted-foreground">{user.email}</span>
                      </div>
                    </SelectItem>
                  ))
                )}
              </SelectContent>
            </Select>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={addMember.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={handleAdd}
            disabled={!selectedUserId || addMember.isPending}
          >
            {addMember.isPending ? 'Adding...' : 'Add Member'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
