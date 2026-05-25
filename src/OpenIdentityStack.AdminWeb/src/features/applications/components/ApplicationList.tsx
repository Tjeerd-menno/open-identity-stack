import { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { DataTable, type Column } from '@/components/common/DataTable';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ApplicationClientType, ApplicationProfile, type ApplicationListItem } from '@/types';
import { Eye, Edit, Plus, Search, Trash2 } from 'lucide-react';
import { useDeleteApplication } from '../hooks/useDeleteApplication';
import { useApplications } from '../hooks/useApplications';
import { ApplicationStatusBadge } from './ApplicationStatusBadge';

const applicationProfileLabels: Record<ApplicationProfile, string> = {
  [ApplicationProfile.Web]: 'Web',
  [ApplicationProfile.SinglePage]: 'Single Page',
  [ApplicationProfile.Native]: 'Native',
  [ApplicationProfile.MachineToMachine]: 'Machine-to-machine',
  [ApplicationProfile.Device]: 'Device',
  [ApplicationProfile.Custom]: 'Custom',
};

const clientTypeLabels: Record<ApplicationClientType, string> = {
  [ApplicationClientType.Confidential]: 'Confidential',
  [ApplicationClientType.Public]: 'Public',
};

export function ApplicationList() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const [applicationToDelete, setApplicationToDelete] = useState<string | null>(null);
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const pageSize = 20;

  const { data, isLoading } = useApplications({ page, pageSize, search: searchTerm });
  const deleteApplication = useDeleteApplication();

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

  const handleDelete = async (applicationId: string) => {
    try {
      await deleteApplication.mutateAsync(applicationId);
      setApplicationToDelete(null);
    } catch (error) {
      console.error('Failed to delete application:', error);
    }
  };

  const columns: Column<ApplicationListItem>[] = [
    {
      header: 'Client ID',
      accessorKey: 'clientId',
    },
    {
      header: 'Display Name',
      accessorKey: 'displayName',
    },
    {
      header: 'Profile',
      accessorKey: 'profile',
      cell: ({ row }) => applicationProfileLabels[row.profile],
    },
    {
      header: 'Client Type',
      accessorKey: 'clientType',
      cell: ({ row }) => clientTypeLabels[row.clientType],
    },
    {
      header: 'Status',
      accessorKey: 'status',
      cell: ({ row }) => <ApplicationStatusBadge status={row.status} />,
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
            onClick={() => navigate(`/applications/${row.id}`)}
            aria-label="View application"
          >
            <Eye className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/applications/${row.id}/edit`)}
            aria-label="Edit application"
          >
            <Edit className="h-4 w-4" />
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setApplicationToDelete(row.id)}
            aria-label="Delete application"
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Applications</h1>
          <p className="text-muted-foreground">
            Manage OAuth 2.0 and OpenID Connect applications
          </p>
        </div>
        <Button onClick={() => navigate('/applications/new')}>
          <Plus className="h-4 w-4 mr-2" />
          New Application
        </Button>
      </div>

      <div className="flex items-center gap-2">
        <Search className="h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Search by client ID or display name..."
          value={search}
          onChange={(event) => handleSearchChange(event.target.value)}
          className="max-w-sm"
        />
      </div>

      <DataTable
        columns={columns}
        data={data?.items || []}
        isLoading={isLoading}
        emptyMessage="No applications found"
        pagination={{
          page,
          pageSize,
          totalCount: data?.totalCount || 0,
          totalPages: data?.totalPages ?? (data ? Math.ceil(data.totalCount / pageSize) : 0),
          onPageChange: setPage,
        }}
      />

      <ConfirmDialog
        open={applicationToDelete !== null}
        onOpenChange={(open) => !open && setApplicationToDelete(null)}
        onConfirm={() => applicationToDelete && handleDelete(applicationToDelete)}
        title="Delete Application"
        description="Are you sure you want to delete this application? This action cannot be undone."
      />
    </div>
  );
}
