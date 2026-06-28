import { Card, Center, Skeleton, Stack, Table, Text } from '@mantine/core';
import type { ReactNode } from 'react';
import { Icon, type IconName } from './Icon';
import { ThemeIcon } from '@mantine/core';

export type Column<T> = {
  key: string;
  header: string;
  align?: 'left' | 'center' | 'right';
  width?: number | string;
  render: (row: T) => ReactNode;
};

type DataTableProps<T> = {
  columns: Column<T>[];
  rows: T[];
  getRowKey: (row: T) => string;
  onRowClick?: (row: T) => void;
  isLoading?: boolean;
  emptyIcon?: IconName;
  emptyTitle?: string;
  emptyText?: string;
  minWidth?: number;
};

/**
 * Full-width table inside a bordered card with an uppercase, letter-spaced
 * sunken header row — the design system's standard list surface.
 */
export function DataTable<T>({
  columns,
  rows,
  getRowKey,
  onRowClick,
  isLoading = false,
  emptyIcon = 'search',
  emptyTitle = 'Nothing to show',
  emptyText = 'Adjust your filters or create a new record.',
  minWidth = 720,
}: DataTableProps<T>) {
  const headerStyle = {
    textTransform: 'uppercase' as const,
    letterSpacing: '0.04em',
    fontSize: 'var(--mantine-font-size-xs)',
    fontWeight: 600,
    color: 'var(--mw-text-muted)',
  };

  return (
    <Card padding={0} style={{ overflow: 'hidden' }}>
      <Table.ScrollContainer minWidth={minWidth}>
        <Table highlightOnHover={!!onRowClick} verticalSpacing="sm" horizontalSpacing="lg">
          <Table.Thead style={{ background: 'var(--mw-surface-sunken)' }}>
            <Table.Tr>
              {columns.map((column) => (
                <Table.Th key={column.key} style={{ textAlign: column.align, width: column.width, ...headerStyle }}>
                  {column.header}
                </Table.Th>
              ))}
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {isLoading ? (
              Array.from({ length: 5 }, (_, index) => (
                <Table.Tr key={index}>
                  {columns.map((column) => (
                    <Table.Td key={column.key}>
                      <Skeleton height={16} />
                    </Table.Td>
                  ))}
                </Table.Tr>
              ))
            ) : rows.length === 0 ? (
              <Table.Tr>
                <Table.Td colSpan={columns.length}>
                  <Center py={48}>
                    <Stack align="center" gap={6}>
                      <ThemeIcon color="gray" radius="md" size={44} variant="light">
                        <Icon name={emptyIcon} size={22} />
                      </ThemeIcon>
                      <Text fw={600} mt={4}>
                        {emptyTitle}
                      </Text>
                      <Text c="dimmed" size="sm">
                        {emptyText}
                      </Text>
                    </Stack>
                  </Center>
                </Table.Td>
              </Table.Tr>
            ) : (
              rows.map((row) => (
                <Table.Tr
                  key={getRowKey(row)}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  style={onRowClick ? { cursor: 'pointer' } : undefined}
                >
                  {columns.map((column) => (
                    <Table.Td key={column.key} style={{ textAlign: column.align }}>
                      {column.render(row)}
                    </Table.Td>
                  ))}
                </Table.Tr>
              ))
            )}
          </Table.Tbody>
        </Table>
      </Table.ScrollContainer>
    </Card>
  );
}
