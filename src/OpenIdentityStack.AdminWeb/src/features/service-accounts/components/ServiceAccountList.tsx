/**
 * ServiceAccountList Component
 * 
 * Displays paginated list of service accounts with search, filter, and actions
 */

import { useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useServiceAccounts } from '../hooks/useServiceAccounts';
import { useDeleteServiceAccount } from '../hooks/useDeleteServiceAccount';
import { useEnableServiceAccount } from '../hooks/useEnableServiceAccount';
import { useDisableServiceAccount } from '../hooks/useDisableServiceAccount';
import { DataTable, type Column } from '@/components/common/DataTable';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ServiceAccountStatusBadge } from './ServiceAccountStatusBadge';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import type { ServiceAccount } from '@/types';
import { Plus, Search, Eye, Edit, Trash2, Ban, CheckCircle } from 'lucide-react';

export function ServiceAccountList() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const pageSize = 20;
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const { data, isLoading } = useServiceAccounts({ page, pageSize, search: searchTerm });
  const deleteServiceAccount = useDeleteServiceAccount();
  const disableServiceAccount = useDisableServiceAccount();
  const enableServiceAccount = useEnableServiceAccount();

  const [accountToDelete, setAccountToDelete] = useState<string | null>(null);
  const [accountToDisable, setAccountToDisable] = useState<string | null>(null);

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

  const handleDelete = async (accountId: string) => {
    try {
      await deleteServiceAccount.mutateAsync(accountId);
      setAccountToDelete(null);
    } catch (error) {
      console.error('Failed to delete service account:', error);
    }
  };

  const handleDisable = async (accountId: string) => {
    try {
      await disableServiceAccount.mutateAsync(accountId);
      setAccountToDisable(null);
    } catch (error) {
      console.error('Failed to disable service account:', error);
    }
  };

  const handleEnable = async (accountId: string) => {
    try {
      await enableServiceAccount.mutateAsync(accountId);
    } catch (error) {
      console.error('Failed to enable service account:', error);
    }
  };

  const columns: Column<ServiceAccount>[] = [
    {
      header: 'Client ID',
      accessorKey: 'clientId',
    },
    {
      header: 'Display Name',
      accessorKey: 'displayName',
    },
    {
      header: 'Status',
      accessorKey: 'status',
      cell: ({ row }) => <ServiceAccountStatusBadge status={row.status} />,
    },
    {
      header: 'Scopes',
      accessorKey: 'allowedScopes',
      cell: ({ row }) => row.allowedScopes.length,
    },
    {
      header: 'Credentials',
      accessorKey: 'credentialCount',
    },
    {
      header: 'Created',
      accessorKey: 'createdAt',
      cell: ({ row }) => new Date(row.createdAt).toLocaleDateString(),
    },
    {
      header: 'Actions',
      cell: ({ row }) => (
        <div className="flex gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/service-accounts/${row.id}`)}
            aria-label="View service account"
          >
            <Eye className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/service-accounts/${row.id}/edit`)}
            aria-label="Edit service account"
          >
            <Edit className="h-4 w-4" />
          </Button>
          {row.status === 'Active' ? (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setAccountToDisable(row.id)}
              aria-label="Disable service account"
            >
              <Ban className="h-4 w-4" />
            </Button>
          ) : (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => handleEnable(row.id)}
              aria-label="Enable service account"
            >
              <CheckCircle className="h-4 w-4" />
            </Button>
          )}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setAccountToDelete(row.id)}
            aria-label="Delete service account"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Service Accounts</h1>
          <p className="text-muted-foreground">
            Manage service accounts for API access
          </p>
        </div>
        <Button onClick={() => navigate('/service-accounts/new')}>
          <Plus className="h-4 w-4 mr-2" />
          New Service Account
        </Button>
      </div>

      {/* Search */}
      <div className="flex items-center gap-2">
        <Search className="h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search by client ID or display name..."
          value={search}
          onChange={(e) => handleSearchChange(e.target.value)}
          className="max-w-sm"
        />
      </div>

      {/* Table */}
      <DataTable
        columns={columns}
        data={data?.items || []}
        isLoading={isLoading}
        pagination={{
          page,
          pageSize,
          totalCount: data?.totalCount || 0,
          totalPages: data ? Math.ceil(data.totalCount / pageSize) : 0,
          onPageChange: setPage,
        }}
      />

      {/* Delete Confirmation Dialog */}
      <ConfirmDialog
        open={accountToDelete !== null}
        onOpenChange={(open) => !open && setAccountToDelete(null)}
        onConfirm={() => accountToDelete && handleDelete(accountToDelete)}
        title="Delete Service Account"
        description="Are you sure you want to delete this service account? This action cannot be undone."
      />

      {/* Disable Confirmation Dialog */}
      <ConfirmDialog
        open={accountToDisable !== null}
        onOpenChange={(open) => !open && setAccountToDisable(null)}
        onConfirm={() => accountToDisable && handleDisable(accountToDisable)}
        title="Disable Service Account"
        description="Are you sure you want to disable this service account? It will no longer be able to authenticate."
      />
    </div>
  );
}
