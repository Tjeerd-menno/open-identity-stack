import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { DataTable, type Column } from '@/components/common/DataTable';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ServicePermissionStatusBadge } from './ServicePermissionStatusBadge';
import { useRegisteredServices } from '../hooks';
import type { RegisteredServiceListItem } from '@/types';
import { Eye, Plus } from 'lucide-react';

export function RegisteredServiceList() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const pageSize = 20;
  const { data, isLoading } = useRegisteredServices({ page, pageSize, search });

  const columns: Column<RegisteredServiceListItem>[] = [
    { header: 'Service', accessorKey: 'serviceIdentifier' },
    { header: 'Display Name', accessorKey: 'displayName' },
    { header: 'Owner', accessorKey: 'ownerId' },
    { header: 'Permissions', accessorKey: 'permissionCount' },
    {
      header: 'Status',
      accessorKey: 'status',
      cell: ({ row }) => <ServicePermissionStatusBadge status={row.status} />,
    },
    {
      header: 'Actions',
      cell: ({ row }) => (
        <Button variant="ghost" size="sm" onClick={() => navigate(`/service-permissions/${row.id}`)}>
          <Eye className="h-4 w-4" />
        </Button>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Service Permission Registry</h1>
          <p className="text-muted-foreground">Register service-owned permissions and control RBAC assignability.</p>
        </div>
        <Button onClick={() => navigate('/service-permissions/new')}>
          <Plus className="mr-2 h-4 w-4" />
          Register Service
        </Button>
      </div>

      <Input
        className="max-w-sm"
        placeholder="Search services..."
        value={search}
        onChange={(event) => {
          setSearch(event.target.value);
          setPage(1);
        }}
      />

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        isLoading={isLoading}
        pagination={{
          page,
          pageSize,
          totalCount: data?.totalCount ?? 0,
          totalPages: data?.totalPages ?? 0,
          onPageChange: setPage,
        }}
      />
    </div>
  );
}
