/**
 * AddMappingDialog Component
 * 
 * Dialog for adding a role or claim mapping to a group
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useRoles } from '@/features/roles/hooks/useRoles';
import { useAddMapping } from '../hooks/useAddMapping';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import type { MappingType } from '@/types';

interface AddMappingDialogProps {
  groupId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function AddMappingDialog({ groupId, open, onOpenChange }: AddMappingDialogProps) {
  const [mappingType, setMappingType] = useState<MappingType | ''>('');
  const [mappingValue, setMappingValue] = useState('');
  const { data: rolesData, isLoading: isLoadingRoles } = useRoles({ 
    page: 1, 
    pageSize: 100,
    search: '' 
  });
  const addMapping = useAddMapping();

  const handleAdd = async () => {
    if (!mappingType || !mappingValue) return;

    try {
      await addMapping.mutateAsync({
        groupId,
        data: {
          type: mappingType as MappingType,
          value: mappingValue,
        },
      });
      setMappingType('');
      setMappingValue('');
      onOpenChange(false);
    } catch (error) {
      console.error('Failed to add mapping:', error);
    }
  };

  const roles = rolesData?.items || [];
  const isRoleType = mappingType === 'Role';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Mapping to Group</DialogTitle>
          <DialogDescription>
            Add a role or claim mapping for this group
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Mapping Type Selector */}
          <div className="space-y-2">
            <Label htmlFor="mapping-type">Mapping Type</Label>
            <Select value={mappingType} onValueChange={(value) => {
              setMappingType(value as MappingType);
              setMappingValue(''); // Reset value when type changes
            }}>
              <SelectTrigger id="mapping-type">
                <SelectValue placeholder="Select type..." />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Role">Role</SelectItem>
                <SelectItem value="Claim">Claim</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Value Selector - Role Dropdown or Claim Input */}
          {mappingType && (
            <div className="space-y-2">
              <Label htmlFor="mapping-value">
                {isRoleType ? 'Select Role' : 'Claim Value'}
              </Label>
              {isRoleType ? (
                <Select value={mappingValue} onValueChange={setMappingValue}>
                  <SelectTrigger id="mapping-value">
                    <SelectValue placeholder="Select a role..." />
                  </SelectTrigger>
                  <SelectContent>
                    {isLoadingRoles ? (
                      <div className="flex justify-center py-6">
                        <LoadingSpinner />
                      </div>
                    ) : roles.length === 0 ? (
                      <SelectItem disabled value="">
                        No roles available
                      </SelectItem>
                    ) : (
                      roles.map((role) => (
                        <SelectItem key={role.id} value={role.id}>
                          {role.displayName}
                        </SelectItem>
                      ))
                    )}
                  </SelectContent>
                </Select>
              ) : (
                <Input
                  id="mapping-value"
                  placeholder="Enter claim value (e.g., user@example.com)"
                  value={mappingValue}
                  onChange={(e) => setMappingValue(e.target.value)}
                />
              )}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={addMapping.isPending}
          >
            Cancel
          </Button>
          <Button
            onClick={handleAdd}
            disabled={!mappingType || !mappingValue || addMapping.isPending}
          >
            {addMapping.isPending ? 'Adding...' : 'Add Mapping'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
