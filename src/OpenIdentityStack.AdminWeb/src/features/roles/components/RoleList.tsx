import { useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { LoadingSpinner } from '@/components/common/LoadingSpinner';
import { RoleTypeBadge } from './RoleTypeBadge';
import { useRoles } from '../hooks/useRoles';

/**
 * Role list component with search and pagination
 * 
 * Displays all roles in a table format with search functionality.
 * Supports navigation to role details and creation.
 */
export function RoleList() {
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const { data, isLoading, error } = useRoles({
    page,
    pageSize: 20,
    search: debouncedSearch,
  });

  // Debounce search input
  const handleSearchChange = (value: string) => {
    setSearch(value);
    if (searchTimerRef.current) {
      clearTimeout(searchTimerRef.current);
    }
    searchTimerRef.current = setTimeout(() => {
      setDebouncedSearch(value);
      setPage(1); // Reset to first page on search
    }, 300);
  };

  const handleRowClick = (roleId: string) => {
    navigate(`/roles/${roleId}`);
  };

  const handleCreateClick = () => {
    navigate('/roles/new');
  };

  if (error) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Error Loading Roles</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-destructive">
            {error instanceof Error ? error.message : 'An error occurred'}
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Roles</h1>
          <p className="text-muted-foreground">
            Manage roles and permissions for your application
          </p>
        </div>
        <Button onClick={handleCreateClick}>
          <Plus className="mr-2 h-4 w-4" />
          Create Role
        </Button>
      </div>

      {/* Search bar */}
      <Card>
        <CardHeader className="pb-3">
          <div className="flex items-center space-x-2">
            <Search className="h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Search roles..."
              value={search}
              onChange={(e) => handleSearchChange(e.target.value)}
              className="max-w-sm"
            />
          </div>
        </CardHeader>
      </Card>

      {/* Roles table */}
      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex justify-center items-center py-12">
              <LoadingSpinner />
            </div>
          ) : !data || data.items.length === 0 ? (
            <div className="text-center py-12">
              <p className="text-muted-foreground">
                {debouncedSearch ? 'No roles found matching your search' : 'No roles found'}
              </p>
            </div>
          ) : (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Name</TableHead>
                    <TableHead>Display Name</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.map((role) => (
                    <TableRow
                      key={role.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => handleRowClick(role.id)}
                    >
                      <TableCell className="font-mono text-sm">{role.name}</TableCell>
                      <TableCell className="font-medium">{role.displayName}</TableCell>
                      <TableCell>
                        <RoleTypeBadge role={role} />
                      </TableCell>
                      <TableCell>
                        {role.isActive ? (
                          <Badge variant="success">Active</Badge>
                        ) : (
                          <Badge variant="secondary">Inactive</Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          variant="ghost"
                          size="sm"
                          aria-label="View role"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleRowClick(role.id);
                          }}
                        >
                          View
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              {/* Pagination */}
              {data.totalPages > 1 && (
                <div className="flex items-center justify-between px-6 py-4 border-t">
                  <div className="text-sm text-muted-foreground">
                    Showing {((data.page - 1) * data.pageSize) + 1} to{' '}
                    {Math.min(data.page * data.pageSize, data.totalCount)} of{' '}
                    {data.totalCount} roles
                  </div>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setPage(page - 1)}
                      disabled={page === 1}
                    >
                      Previous
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setPage(page + 1)}
                      disabled={page === data.totalPages}
                    >
                      Next
                    </Button>
                  </div>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
