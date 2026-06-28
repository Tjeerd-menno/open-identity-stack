import { Group, Pagination, Select, TextInput, Text } from '@mantine/core';
import { Icon } from './Icon';

export function SearchInput({
  value,
  onChange,
  placeholder = 'Search…',
  width = 320,
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  width?: number | string;
}) {
  return (
    <TextInput
      aria-label={placeholder}
      leftSection={<Icon name="search" size={16} />}
      placeholder={placeholder}
      value={value}
      w={width}
      onChange={(event) => onChange(event.currentTarget.value)}
    />
  );
}

export function Pager({
  page,
  pageSize,
  totalCount,
  totalPages,
  onPageChange,
  onPageSizeChange,
}: {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  onPageSizeChange?: (pageSize: number) => void;
}) {
  if (totalCount === 0) {
    return null;
  }
  const from = (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalCount);
  return (
    <Group justify="space-between" mt="md" wrap="wrap">
      <Text c="dimmed" size="sm">
        {from.toLocaleString()}–{to.toLocaleString()} of {totalCount.toLocaleString()}
      </Text>
      <Group gap="sm">
        {onPageSizeChange && (
          <Select
            allowDeselect={false}
            aria-label="Rows per page"
            data={['10', '25', '50', '100']}
            value={String(pageSize)}
            w={84}
            onChange={(value) => value && onPageSizeChange(Number(value))}
          />
        )}
        {totalPages > 1 && <Pagination total={totalPages} value={page} onChange={onPageChange} size="sm" />}
      </Group>
    </Group>
  );
}
