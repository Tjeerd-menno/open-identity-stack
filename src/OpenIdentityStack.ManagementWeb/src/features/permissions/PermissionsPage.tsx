import { Badge, Box, Text } from '@mantine/core';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import type { RegisteredApplicationListItem } from '@openidentitystack/admin-api-client';
import { DataTable, type Column } from '@/components/DataTable';
import { FilterToolbar } from '@/components/FilterToolbar';
import { Pager } from '@/components/ListControls';
import { ErrorState, PageHeader, StatusBadge } from '@/components/primitives';
import { api, getApiErrorMessage } from '@/lib/api';

export function PermissionsPage() {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  const query = useQuery({
    queryKey: ['application-permissions', { page, pageSize, search }],
    queryFn: () => api.applicationPermissions.getRegisteredApplications({ page, pageSize, search: search || undefined }),
  });

  const columns: Column<RegisteredApplicationListItem>[] = [
    {
      key: 'app',
      header: 'Application',
      render: (item) => (
        <Box style={{ minWidth: 0 }}>
          <Text fw={600} size="sm" truncate>
            {item.displayName}
          </Text>
          <Text c="dimmed" className="mw-mono" size="xs" truncate>
            {item.applicationIdentifier}
          </Text>
        </Box>
      ),
    },
    {
      key: 'owner',
      header: 'Owner',
      render: (item) => (
        <Badge color="gray" variant="outline">
          {item.ownerType}
        </Badge>
      ),
    },
    {
      key: 'permissions',
      header: 'Permissions',
      align: 'right',
      render: (item) => (
        <Badge color="blue" variant="light">
          {item.permissionCount}
        </Badge>
      ),
    },
    { key: 'status', header: 'Status', render: (item) => <StatusBadge status={item.status} /> },
  ];

  return (
    <div>
      <PageHeader
        title="Permissions"
        description="Resource servers that declare the permissions (scopes) applications can be granted."
      />

      <FilterToolbar
        noun="applications"
        search={search}
        onSearch={(value) => { setSearch(value); setPage(1); }}
        rows={pageSize}
        onRows={(size) => { setPageSize(size); setPage(1); }}
        count={query.data?.totalCount}
      />

      {query.isError ? (
        <ErrorState message={getApiErrorMessage(query.error)} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={query.data?.items ?? []}
            getRowKey={(item) => item.id}
            onRowClick={(item) => navigate(`/application-permissions/${item.id}`)}
            isLoading={query.isLoading}
            emptyIcon="list-checks"
            emptyTitle="No registered applications"
            emptyText="No applications have declared permissions yet."
          />
          <Pager
            page={page}
            pageSize={pageSize}
            totalCount={query.data?.totalCount ?? 0}
            totalPages={query.data?.totalPages ?? 0}
            onPageChange={setPage}
            onPageSizeChange={(size) => { setPageSize(size); setPage(1); }}
          />
        </>
      )}
    </div>
  );
}
