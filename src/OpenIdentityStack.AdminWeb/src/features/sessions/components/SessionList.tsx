/**
 * SessionList Component
 * 
 * Displays paginated list of sessions with search, filter, and actions
 */

import { useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { useSessions } from '../hooks/useSessions';
import { useRevokeSession } from '../hooks/useRevokeSession';
import { DataTable, type Column } from '@/components/common/DataTable';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { SessionStatusBadge } from './SessionStatusBadge';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import type { Session, SessionStatus } from '@/types';
import { Search, Eye, Ban, Filter } from 'lucide-react';

export function SessionList() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<SessionStatus | 'All'>('Active');
  const pageSize = 20;
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const { data, isLoading } = useSessions({ 
    page, 
    pageSize, 
    search: searchTerm,
    status: statusFilter === 'All' ? undefined : statusFilter,
  });
  const revokeSession = useRevokeSession();

  const [sessionToRevoke, setSessionToRevoke] = useState<string | null>(null);

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

  const handleStatusFilterChange = (value: string) => {
    setStatusFilter(value as SessionStatus | 'All');
    setPage(1);
  };

  const handleRevoke = async (sessionId: string) => {
    try {
      await revokeSession.mutateAsync(sessionId);
      setSessionToRevoke(null);
    } catch (error) {
      console.error('Failed to revoke session:', error);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleString();
  };

  const columns: Column<Session>[] = [
    {
      header: 'User ID',
      accessorKey: 'userId',
      cell: ({ row }) => <span className="font-mono text-xs">{row.userId.substring(0, 8)}...</span>,
    },
    {
      header: 'IP Address',
      accessorKey: 'ipAddress',
    },
    {
      header: 'User Agent',
      accessorKey: 'userAgent',
      cell: ({ row }) => <span className="truncate max-w-xs block">{row.userAgent}</span>,
    },
    {
      header: 'Status',
      accessorKey: 'status',
      cell: ({ row }) => <SessionStatusBadge status={row.status} />,
    },
    {
      header: 'Clients',
      accessorKey: 'clientCount',
    },
    {
      header: 'Last Activity',
      accessorKey: 'lastActivityAt',
      cell: ({ row }) => formatDate(row.lastActivityAt),
    },
    {
      header: 'Expires',
      accessorKey: 'expiresAt',
      cell: ({ row }) => formatDate(row.expiresAt),
    },
    {
      header: 'Actions',
      cell: ({ row }) => (
        <div className="flex gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate(`/sessions/${row.id}`)}
            aria-label="View session"
          >
            <Eye className="h-4 w-4" />
          </Button>
          {row.status === 'Active' && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setSessionToRevoke(row.id)}
              aria-label="Revoke session"
            >
              <Ban className="h-4 w-4" />
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Sessions</h1>
          <p className="text-muted-foreground">
            View and manage active user sessions
          </p>
        </div>
      </div>

      {/* Search and Filter */}
      <div className="flex items-center gap-4">
        <div className="flex items-center gap-2 flex-1">
          <Search className="h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search by user ID or IP address..."
            value={search}
            onChange={(e) => handleSearchChange(e.target.value)}
            className="max-w-sm"
          />
        </div>
        <div className="flex items-center gap-2">
          <Filter className="h-4 w-4 text-muted-foreground" />
          <Select value={statusFilter} onValueChange={handleStatusFilterChange}>
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="Filter by status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="All">All Sessions</SelectItem>
              <SelectItem value="Active">Active</SelectItem>
              <SelectItem value="LoggedOut">Logged Out</SelectItem>
              <SelectItem value="Expired">Expired</SelectItem>
              <SelectItem value="Revoked">Revoked</SelectItem>
            </SelectContent>
          </Select>
        </div>
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

      {/* Revoke Confirmation Dialog */}
      <ConfirmDialog
        open={sessionToRevoke !== null}
        onOpenChange={(open) => !open && setSessionToRevoke(null)}
        onConfirm={() => sessionToRevoke && handleRevoke(sessionToRevoke)}
        title="Revoke Session"
        description="Are you sure you want to revoke this session? The user will be immediately logged out."
      />
    </div>
  );
}
