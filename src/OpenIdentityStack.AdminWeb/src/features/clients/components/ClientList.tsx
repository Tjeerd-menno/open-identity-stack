/**
 * ClientList Component
 * 
 * Displays paginated list of clients with search, filter, and actions
 */

import { useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useClients } from '../hooks/useClients';
import { useDeleteClient } from '../hooks/useDeleteClient';
import { DataTable, type Column } from '@/components/common/DataTable';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import type { ClientListItem, ClientType } from '@/types';
import { Plus, Search, Eye, Edit, Trash2 } from 'lucide-react';

export function ClientList() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const pageSize = 20;
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const { data, isLoading } = useClients({ page, pageSize, search: searchTerm });
  const deleteClient = useDeleteClient();

  const [clientToDelete, setClientToDelete] = useState<string | null>(null);

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

  const handleDelete = async (clientId: string) => {
    try {
      await deleteClient.mutateAsync(clientId);
      setClientToDelete(null);
    } catch (error) {
      console.error('Failed to delete client:', error);
    }
  };

  const getClientTypeLabel = (clientType: ClientType): string => {
    return clientType === 0 ? 'Confidential' : 'Public';
  };

  const columns: Column<ClientListItem>[] = [
    {
      header: 'Client ID',
      accessorKey: 'clientIdValue',
    },
    {
      header: 'Display Name',
      accessorKey: 'displayName',
    },
    {
      header: 'Client Type',
      accessorKey: 'clientType',
      cell: ({ row }) => getClientTypeLabel(row.clientType),
    },
    {
      header: 'Grant Types',
      accessorKey: 'allowedGrantTypes',
      cell: ({ row }) => row.allowedGrantTypes.join(', '),
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
            onClick={() => navigate(`/clients/${row.id}`)}
            aria-label="View client"
          >
            <Eye className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/clients/${row.id}/edit`)}
            aria-label="Edit client"
          >
            <Edit className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setClientToDelete(row.id)}
            aria-label="Delete client"
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
          <h1 className="text-3xl font-bold">Clients</h1>
          <p className="text-muted-foreground">
            Manage OAuth 2.0 and OpenID Connect clients
          </p>
        </div>
        <Button onClick={() => navigate('/clients/new')}>
          <Plus className="h-4 w-4 mr-2" />
          New Client
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
        open={clientToDelete !== null}
        onOpenChange={(open) => !open && setClientToDelete(null)}
        onConfirm={() => clientToDelete && handleDelete(clientToDelete)}
        title="Delete Client"
        description="Are you sure you want to delete this client? This action cannot be undone."
      />
    </div>
  );
}
