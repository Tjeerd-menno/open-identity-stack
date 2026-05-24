import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { DataTable, type Column } from '@/components/common/DataTable';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ApplicationPermissionStatusBadge } from './ApplicationPermissionStatusBadge';
import { useRegisteredApplications } from '../hooks';
import type { RegisteredApplicationListItem } from '@/types';
import { Eye, Plus } from 'lucide-react';

export function RegisteredApplicationList() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const pageSize = 20;
  const { data, isLoading } = useRegisteredApplications({ page, pageSize, search });

  const columns: Column<RegisteredApplicationListItem>[] = [
    { header: 'Service', accessorKey: 'applicationIdentifier' },
    { header: 'Display Name', accessorKey: 'displayName' },
    { header: 'Owner', accessorKey: 'ownerId' },
    { header: 'Permissions', accessorKey: 'permissionCount' },
    {
      header: 'Status',
      accessorKey: 'status',
      cell: ({ row }) => <ApplicationPermissionStatusBadge status={row.status} />,
    },
    {
      header: 'Actions',
      cell: ({ row }) => (
        <Button variant="ghost" size="sm" onClick={() => navigate(`/application-permissions/${row.id}`)}>
          <Eye className="h-4 w-4" />
        </Button>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Permissions</h1>
        </div>
        <Button onClick={() => navigate('/application-permissions/new')}>
          <Plus className="mr-2 h-4 w-4" />
          Add Application
        </Button>
      </div>

      <Input
        className="max-w-sm"
        placeholder="Search applications..."
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
