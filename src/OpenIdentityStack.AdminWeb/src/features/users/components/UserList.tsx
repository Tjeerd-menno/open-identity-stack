/**
 * UserList Component
 * 
 * Displays paginated list of users with search, filter, and actions
 */

import { useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useUsers } from '../hooks/useUsers';
import { useDeleteUser } from '../hooks/useDeleteUser';
import { useDisableUser } from '../hooks/useDisableUser';
import { useEnableUser } from '../hooks/useEnableUser';
import { DataTable, type Column } from '@/components/common/DataTable';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { UserStatusBadge } from './UserStatusBadge';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import type { UserListItem } from '@/types';
import { Plus, Search, Eye, Edit, Trash2, Ban, CheckCircle } from 'lucide-react';

export function UserList() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const pageSize = 20;
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const { data, isLoading } = useUsers({ page, pageSize, search: searchTerm });
  const deleteUser = useDeleteUser();
  const disableUser = useDisableUser();
  const enableUser = useEnableUser();

  const [userToDelete, setUserToDelete] = useState<string | null>(null);
  const [userToDisable, setUserToDisable] = useState<string | null>(null);

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

  const handleDelete = async (userId: string) => {
    try {
      await deleteUser.mutateAsync(userId);
      setUserToDelete(null);
    } catch (error) {
      console.error('Failed to delete user:', error);
    }
  };

  const handleDisable = async (userId: string) => {
    try {
      await disableUser.mutateAsync({ 
        userId, 
        data: { reason: 'Disabled by administrator' } 
      });
      setUserToDisable(null);
    } catch (error) {
      console.error('Failed to disable user:', error);
    }
  };

  const handleEnable = async (userId: string) => {
    try {
      await enableUser.mutateAsync(userId);
    } catch (error) {
      console.error('Failed to enable user:', error);
    }
  };

  const columns: Column<UserListItem>[] = [
    {
      header: 'Email',
      accessorKey: 'email',
    },
    {
      header: 'Display Name',
      accessorKey: 'displayName',
    },
    {
      header: 'Status',
      cell: ({ row }) => <UserStatusBadge status={row.status} />,
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
            onClick={() => navigate(`/users/${row.id}`)}
            aria-label="View user"
          >
            <Eye className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/users/${row.id}/edit`)}
            aria-label="Edit user"
          >
            <Edit className="h-4 w-4" />
          </Button>
          {row.status === 'Active' ? (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setUserToDisable(row.id)}
              aria-label="Disable user"
            >
              <Ban className="h-4 w-4" />
            </Button>
          ) : row.status === 'Disabled' ? (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => handleEnable(row.id)}
              aria-label="Enable user"
            >
              <CheckCircle className="h-4 w-4" />
            </Button>
          ) : null}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setUserToDelete(row.id)}
            aria-label="Delete user"
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
        <h1 className="text-3xl font-bold">Users</h1>
        <Button onClick={() => navigate('/users/create')}>
          <Plus className="h-4 w-4 mr-2" />
          Create User
        </Button>
      </div>

      <div className="flex items-center gap-4">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search users by email or name..."
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
        emptyMessage="No users found"
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
        open={!!userToDelete}
        onOpenChange={(open) => !open && setUserToDelete(null)}
        title="Delete User"
        description="Are you sure you want to permanently delete this user? This action cannot be undone."
        onConfirm={() => userToDelete && handleDelete(userToDelete)}
        confirmLabel="Delete"
        variant="destructive"
      />

      {/* Disable Confirmation Dialog */}
      <ConfirmDialog
        open={!!userToDisable}
        onOpenChange={(open) => !open && setUserToDisable(null)}
        title="Disable User"
        description="Are you sure you want to disable this user? They will not be able to log in."
        onConfirm={() => userToDisable && handleDisable(userToDisable)}
        confirmLabel="Disable"
        variant="destructive"
      />
    </div>
  );
}
