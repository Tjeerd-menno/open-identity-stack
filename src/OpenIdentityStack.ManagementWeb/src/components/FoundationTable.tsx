import { Alert, Button, Center, Group, Loader, Table, Text } from '@mantine/core';
import type { ReactNode } from 'react';

export type FoundationColumn<T> = {
  header: string;
  accessorKey?: keyof T;
  cell?: (row: T) => ReactNode;
};

type FoundationTableProps<T extends { id: string }> = {
  columns: FoundationColumn<T>[];
  data: T[];
  isLoading?: boolean;
  error?: string | null;
  emptyMessage?: string;
  pagination?: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    onPageChange: (page: number) => void;
  };
};

export function FoundationTable<T extends { id: string }>({
  columns,
  data,
  isLoading = false,
  error,
  emptyMessage = 'No records found',
  pagination,
}: FoundationTableProps<T>) {
  if (isLoading) {
    return (
      <Center py="xl">
        <Loader aria-label="Loading table data" />
      </Center>
    );
  }

  if (error) {
    return <Alert color="red">{error}</Alert>;
  }

  return (
    <>
      <Table striped highlightOnHover withTableBorder withColumnBorders>
        <Table.Thead>
          <Table.Tr>
            {columns.map((column) => (
              <Table.Th key={column.header}>{column.header}</Table.Th>
            ))}
          </Table.Tr>
        </Table.Thead>
        <Table.Tbody>
          {data.length === 0 ? (
            <Table.Tr>
              <Table.Td colSpan={columns.length}>
                <Center py="lg">
                  <Text c="dimmed">{emptyMessage}</Text>
                </Center>
              </Table.Td>
            </Table.Tr>
          ) : (
            data.map((row) => (
              <Table.Tr key={row.id}>
                {columns.map((column) => (
                  <Table.Td key={column.header}>
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

      {pagination && pagination.totalPages > 1 && (
        <Group justify="space-between" mt="md">
          <Text size="sm" c="dimmed">
            Showing {(pagination.page - 1) * pagination.pageSize + 1} to{' '}
            {Math.min(pagination.page * pagination.pageSize, pagination.totalCount)} of{' '}
            {pagination.totalCount} results
          </Text>
          <Group gap="xs">
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
    </>
  );
}
