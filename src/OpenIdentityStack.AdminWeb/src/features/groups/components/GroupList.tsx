/**
 * GroupList Component
 * 
 * Displays paginated list of groups with search, filter, and actions
 */

import { useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useGroups } from '../hooks/useGroups';
import { useDeleteGroup } from '../hooks/useDeleteGroup';
import { DataTable, type Column } from '@/components/common/DataTable';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import type { GroupListItem } from '@/types';
import { Plus, Search, Eye, Edit, Trash2 } from 'lucide-react';

export function GroupList() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const pageSize = 20;
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const { data, isLoading } = useGroups({ page, pageSize, search: searchTerm });
  const deleteGroup = useDeleteGroup();

  const [groupToDelete, setGroupToDelete] = useState<string | null>(null);

  // Debounce search
  const handleSearchChange = (value: string) => {
    setSearch(value);
    if (searchTimerRef.current) {
      clearTimeout(searchTimerRef.current);
    }
    searchTimerRef.current = setTimeout(() => {
      setSearchTerm(value);
      setPage(1);
    }, 300);
  };

  const handleDelete = async (groupId: string) => {
    try {
      await deleteGroup.mutateAsync(groupId);
      setGroupToDelete(null);
    } catch (error) {
      console.error('Failed to delete group:', error);
    }
  };

  const columns: Column<GroupListItem>[] = [
    {
      header: 'Name',
      accessorKey: 'name',
    },
    {
      header: 'Description',
      accessorKey: 'description',
    },
    {
      header: 'Members',
      cell: ({ row }) => row.memberCount,
    },
    {
      header: 'Mappings',
      cell: ({ row }) => row.mappingCount,
    },
    {
      header: 'Created',
      cell: ({ row }) => new Date(row.createdAt).toLocaleDateString(),
    },
    {
      header: 'Actions',
      cell: ({ row }) => (
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/groups/${row.id}`)}
            aria-label="View group"
          >
            <Eye className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/groups/${row.id}/edit`)}
            aria-label="Edit group"
          >
            <Edit className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setGroupToDelete(row.id)}
            aria-label="Delete group"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      ),
      className: 'text-right',
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-3xl font-bold">Groups</h1>
        <Button onClick={() => navigate('/groups/new')}>
          <Plus className="h-4 w-4 mr-2" />
          Create Group
        </Button>
      </div>

      <div className="flex items-center gap-4">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search groups by name or description..."
            value={search}
            onChange={(e) => handleSearchChange(e.target.value)}
            className="pl-10"
          />
        </div>
      </div>

      <DataTable
        columns={columns}
        data={data?.items || []}
        isLoading={isLoading}
        emptyMessage="No groups found"
        pagination={
          data
            ? {
                page: data.page,
                pageSize: data.pageSize,
                totalPages: data.totalPages,
                totalCount: data.totalCount,
                onPageChange: setPage,
              }
            : undefined
        }
      />

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        open={!!groupToDelete}
        onOpenChange={(open) => !open && setGroupToDelete(null)}
        title="Delete Group"
        description="Are you sure you want to permanently delete this group? This action cannot be undone."
        onConfirm={() => groupToDelete && handleDelete(groupToDelete)}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteGroup.isPending}
      />
    </div>
  );
}
