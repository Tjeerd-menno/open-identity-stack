/**
 * GroupMappingsList Component
 * 
 * Displays list of role/claim mappings for a group with add/remove actions
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useGroupMappings } from '../hooks/useGroupMappings';
import { useRemoveMapping } from '../hooks/useRemoveMapping';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { AddMappingDialog } from './AddMappingDialog';
import { X, Plus } from 'lucide-react';
import type { MappingType } from '@/types';

interface GroupMappingsListProps {
  groupId: string;
}

export function GroupMappingsList({ groupId }: GroupMappingsListProps) {
  const { data, isLoading } = useGroupMappings(groupId);
  const removeMapping = useRemoveMapping();
  const [showAddDialog, setShowAddDialog] = useState(false);
  const [mappingToRemove, setMappingToRemove] = useState<string | null>(null);

  const handleRemove = async (mappingId: string) => {
    try {
      await removeMapping.mutateAsync({ groupId, mappingId });
      setMappingToRemove(null);
    } catch (error) {
      console.error('Failed to remove mapping:', error);
    }
  };

  const getMappingTypeBadgeVariant = (type: MappingType): 'default' | 'secondary' | 'outline' => {
    switch (type) {
      case 'Role':
        return 'default';
      case 'Claim':
        return 'secondary';
      default:
        return 'outline';
    }
  };

  if (isLoading) {
    return <LoadingSpinner />;
  }

  const mappings = data?.items || [];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-lg font-semibold">Mappings ({mappings.length})</h3>
        <Button 
          onClick={() => setShowAddDialog(true)}
          size="sm"
        >
          <Plus className="h-4 w-4 mr-1" />
          Add Mapping
        </Button>
      </div>

      {mappings.length === 0 ? (
        <p className="text-sm text-muted-foreground">No mappings configured for this group</p>
      ) : (
        <div className="space-y-2">
          {mappings.map((mapping) => (
            <div
              key={mapping.id}
              className="flex items-center justify-between p-3 border rounded-md hover:bg-muted/50 transition-colors"
              data-testid="mapping-item"
            >
              <div className="flex-1 flex items-center gap-3">
                <Badge variant={getMappingTypeBadgeVariant(mapping.type)}>
                  {mapping.type}
                </Badge>
                <div className="font-mono text-sm">{mapping.value}</div>
                <div className="text-xs text-muted-foreground ml-auto">
                  {new Date(mapping.createdAt).toLocaleDateString()}
                </div>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setMappingToRemove(mapping.id)}
                aria-label={`Remove mapping ${mapping.value}`}
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          ))}
        </div>
      )}

      {/* Add Mapping Dialog */}
      <AddMappingDialog
        groupId={groupId}
        open={showAddDialog}
        onOpenChange={setShowAddDialog}
      />

      {/* Remove Mapping Confirmation Dialog */}
      <ConfirmDialog
        open={!!mappingToRemove}
        onOpenChange={(open) => !open && setMappingToRemove(null)}
        title="Remove Mapping"
        description="Are you sure you want to remove this mapping from the group?"
        onConfirm={() => mappingToRemove && handleRemove(mappingToRemove)}
        confirmLabel="Remove"
        variant="destructive"
        loading={removeMapping.isPending}
      />
    </div>
  );
}
