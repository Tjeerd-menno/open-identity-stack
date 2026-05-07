import { useState, useMemo, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useProviders, useDeleteProvider } from '../hooks';
import { DataTable, type Column } from '@/components/common/DataTable';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { ProviderStatus, type Provider } from '@/types';
import { Plus, Eye, Trash2 } from 'lucide-react';

export function ProviderList() {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }
    debounceRef.current = setTimeout(() => {
      setDebouncedSearch(search);
    }, 300);
    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
  }, [search]);

  const { data, isLoading } = useProviders(true);
  const deleteMutation = useDeleteProvider();

  const filteredData = useMemo(() => {
    if (!data) return [];
    if (!debouncedSearch) return data;
    const lowerSearch = debouncedSearch.toLowerCase();
    return data.filter(
      (p: Provider) =>
        p.name.toLowerCase().includes(lowerSearch) ||
        p.displayName.toLowerCase().includes(lowerSearch)
    );
  }, [data, debouncedSearch]);

  const handleDelete = async () => {
    if (deleteId) {
      await deleteMutation.mutateAsync(deleteId);
      setDeleteId(null);
    }
  };

  const columns: Column<Provider>[] = [
    {
      header: 'Name',
      accessorKey: 'displayName',
      cell: ({ row }: { row: Provider }) => (
        <div className="font-medium">{row.displayName}</div>
      )
    },
    {
      header: 'Authority',
      accessorKey: 'authority',
      cell: ({ row }: { row: Provider }) => (
        <div className="text-sm text-muted-foreground">{row.authority || '-'}</div>
      )
    },
    {
      header: 'Status',
      accessorKey: 'status',
      cell: ({ row }: { row: Provider }) => (
        <Badge variant={row.status === ProviderStatus.Active ? 'default' : 'secondary'}>
          {row.status === ProviderStatus.Active ? 'Active' : 'Disabled'}
        </Badge>
      )
    },
    {
      header: 'JIT Provisioning',
      accessorKey: 'jitProvisioningEnabled',
      cell: ({ row }: { row: Provider }) => (
        <Badge variant={row.jitProvisioningEnabled ? 'outline' : 'secondary'}>
          {row.jitProvisioningEnabled ? 'Enabled' : 'Disabled'}
        </Badge>
      )
    },
    {
      header: 'Actions',
      id: 'actions',
      cell: ({ row }: { row: Provider }) => (
        <div className="flex gap-2">
          <Button
            size="sm"
            variant="ghost"
            onClick={() => navigate(`/providers/${row.id}`)}
            aria-label="View provider"
          >
            <Eye className="h-4 w-4" />
          </Button>
          <Button
            size="sm"
            variant="ghost"
            onClick={() => setDeleteId(row.id)}
            aria-label="Delete provider"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      )
    }
  ];

  return (
    <div className="space-y-4">
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-bold">Providers</h1>
        <Button onClick={() => navigate('/providers/new')}>
          <Plus className="mr-2 h-4 w-4" />
          Add Provider
        </Button>
      </div>

      <DataTable
        columns={columns}
        data={filteredData}
        isLoading={isLoading}
        search={{
          value: search,
          onChange: setSearch,
          placeholder: 'Search providers...'
        }}
      />

      <ConfirmDialog
        open={deleteId !== null}
        onOpenChange={(open) => {
          if (!open) setDeleteId(null);
        }}
        onConfirm={handleDelete}
        title="Delete Provider"
        description="Are you sure you want to delete this identity provider? This action cannot be undone."
        variant="destructive"
      />
    </div>
  );
}
