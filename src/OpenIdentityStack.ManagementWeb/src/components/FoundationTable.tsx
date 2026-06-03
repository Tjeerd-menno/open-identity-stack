import { Alert, Button, Center, Checkbox, Group, Select, Skeleton, Stack, Table, Text } from '@mantine/core';
import type { ReactNode } from 'react';

export type FoundationColumn<T> = {
  header: string;
  accessorKey?: keyof T;
  cell?: (row: T) => ReactNode;
  sortable?: boolean;
  align?: 'left' | 'center' | 'right';
  width?: number | string;
};

type FoundationTableProps<T extends { id: string }> = {
  columns: FoundationColumn<T>[];
  data: T[];
  isLoading?: boolean;
  error?: string | null;
  emptyMessage?: string;
  toolbar?: ReactNode;
  bulkActions?: ReactNode;
  density?: 'compact' | 'comfortable';
  minWidth?: number;
  selectedRowIds?: string[];
  onSelectedRowIdsChange?: (ids: string[]) => void;
  getRowLabel?: (row: T) => string;
  sort?: {
    column: string;
    direction: 'asc' | 'desc';
    onSortChange: (column: string) => void;
  };
  pagination?: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    onPageChange: (page: number) => void;
    onPageSizeChange?: (pageSize: number) => void;
    pageSizeOptions?: number[];
  };
};

export function FoundationTable<T extends { id: string }>({
  columns,
  data,
  isLoading = false,
  error,
  emptyMessage = 'No records found',
  toolbar,
  bulkActions,
  density = 'compact',
  minWidth = 760,
  selectedRowIds,
  onSelectedRowIdsChange,
  getRowLabel,
  sort,
  pagination,
}: FoundationTableProps<T>) {
  if (error) {
    return <Alert color="red">{error}</Alert>;
  }

  const isSelectable = Boolean(selectedRowIds && onSelectedRowIdsChange);
  const selected = new Set(selectedRowIds ?? []);
  const allVisibleSelected = data.length > 0 && data.every((row) => selected.has(row.id));
  const visibleIds = data.map((row) => row.id);

  function toggleVisibleRows(checked: boolean) {
    if (!onSelectedRowIdsChange) {
      return;
    }

    const next = new Set(selected);
    visibleIds.forEach((id) => {
      if (checked) {
        next.add(id);
      } else {
        next.delete(id);
      }
    });
    onSelectedRowIdsChange(Array.from(next));
  }

  function toggleRow(row: T, checked: boolean) {
    if (!onSelectedRowIdsChange) {
      return;
    }

    const next = new Set(selected);
    if (checked) {
      next.add(row.id);
    } else {
      next.delete(row.id);
    }
    onSelectedRowIdsChange(Array.from(next));
  }

  return (
    <Stack gap="sm">
      {toolbar}
      {bulkActions && selected.size > 0 && (
        <Group justify="space-between">
          <Text size="sm" c="dimmed">{selected.size} selected</Text>
          {bulkActions}
        </Group>
      )}
      {isLoading && <span aria-label="Loading table data" role="status" />}
      <Table.ScrollContainer minWidth={minWidth}>
        <Table
          highlightOnHover
          striped
          verticalSpacing={density === 'compact' ? 'xs' : 'sm'}
          withColumnBorders={false}
          withTableBorder
        >
          <Table.Thead>
            <Table.Tr>
              {isSelectable && (
                <Table.Th w={42}>
                  <Checkbox
                    aria-label="Select visible rows"
                    checked={allVisibleSelected}
                    disabled={data.length === 0}
                    onChange={(event) => toggleVisibleRows(event.currentTarget.checked)}
                  />
                </Table.Th>
              )}
              {columns.map((column) => (
                <Table.Th key={column.header} style={{ textAlign: column.align, width: column.width }}>
                  {column.sortable && sort ? (
                    <Button
                      aria-label={`Sort by ${column.header}`}
                      size="xs"
                      variant="subtle"
                      onClick={() => sort.onSortChange(column.header)}
                    >
                      {column.header}{sort.column === column.header ? ` ${sort.direction === 'asc' ? 'ascending' : 'descending'}` : ''}
                    </Button>
                  ) : (
                    column.header
                  )}
                </Table.Th>
              ))}
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {isLoading ? (
              Array.from({ length: 5 }, (_, index) => (
                <Table.Tr key={index}>
                  {isSelectable && (
                    <Table.Td>
                      <Skeleton height={18} width={18} />
                    </Table.Td>
                  )}
                  {columns.map((column) => (
                    <Table.Td key={column.header}>
                      <Skeleton height={18} />
                    </Table.Td>
                  ))}
                </Table.Tr>
              ))
            ) : data.length === 0 ? (
              <Table.Tr>
                <Table.Td colSpan={columns.length + (isSelectable ? 1 : 0)}>
                  <Center py="xl">
                    <Stack gap={4} align="center">
                      <Text fw={600}>{emptyMessage}</Text>
                      <Text size="sm" c="dimmed">Adjust filters or create a new record if you have access.</Text>
                    </Stack>
                  </Center>
                </Table.Td>
              </Table.Tr>
            ) : (
              data.map((row) => (
                <Table.Tr key={row.id}>
                  {isSelectable && (
                    <Table.Td>
                      <Checkbox
                        aria-label={`Select ${getRowLabel?.(row) ?? row.id}`}
                        checked={selected.has(row.id)}
                        onChange={(event) => toggleRow(row, event.currentTarget.checked)}
                      />
                    </Table.Td>
                  )}
                  {columns.map((column) => (
                    <Table.Td key={column.header} style={{ textAlign: column.align }}>
                      {column.cell
                        ? column.cell(row)
                        : column.accessorKey
                          ? String(row[column.accessorKey] ?? '')
                          : null}
                    </Table.Td>
                  ))}
                </Table.Tr>
              ))
            )}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>

      {pagination && pagination.totalPages > 1 && (
        <Group justify="space-between" mt="md">
          <Text size="sm" c="dimmed">
            Showing {(pagination.page - 1) * pagination.pageSize + 1} to{' '}
            {Math.min(pagination.page * pagination.pageSize, pagination.totalCount)} of{' '}
            {pagination.totalCount} results
          </Text>
          <Group gap="xs">
            {pagination.onPageSizeChange && (
              <Select
                allowDeselect={false}
                aria-label="Rows per page"
                data={(pagination.pageSizeOptions ?? [10, 20, 50, 100]).map((option) => ({
                  value: String(option),
                  label: `${option} / page`,
                }))}
                size="xs"
                value={String(pagination.pageSize)}
                w={125}
                onChange={(value) => value && pagination.onPageSizeChange?.(Number(value))}
              />
            )}
            <Button
              variant="default"
              disabled={pagination.page === 1}
              onClick={() => pagination.onPageChange(pagination.page - 1)}
            >
              Previous
            </Button>
            <Text size="sm">
              Page {pagination.page} of {pagination.totalPages}
            </Text>
            <Button
              variant="default"
              disabled={pagination.page >= pagination.totalPages}
              onClick={() => pagination.onPageChange(pagination.page + 1)}
            >
              Next
            </Button>
          </Group>
        </Group>
      )}
    </Stack>
  );
}
