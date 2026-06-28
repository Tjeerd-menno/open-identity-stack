import { ActionIcon, Box, Group, NativeSelect, Select, Text, TextInput } from '@mantine/core';
import { Icon } from './Icon';

export type ToolbarFilter = {
  key: string;
  label: string;
  value: string;
  options: string[];
  onChange: (value: string) => void;
};

type FilterToolbarProps = {
  noun: string;
  search: string;
  onSearch: (value: string) => void;
  filters?: ToolbarFilter[];
  rows: number;
  onRows: (rows: number) => void;
  count?: number;
  total?: number;
};

const ALL = 'All';

export function FilterToolbar({ noun, search, onSearch, filters = [], rows, onRows, count, total }: FilterToolbarProps) {
  const applied = filters.filter((filter) => filter.value && filter.value !== ALL);

  return (
    <Box mb="md">
      <Group align="flex-end" gap="md" wrap="wrap">
        <Box style={{ flex: '1 1 240px', maxWidth: 320 }}>
          <Text c="dimmed" fw={600} mb={4} size="xs" tt="uppercase" style={{ letterSpacing: '0.03em' }}>
            Search
          </Text>
          <TextInput
            aria-label={`Search ${noun}`}
            leftSection={<Icon name="search" size={16} />}
            placeholder={`Search ${noun}…`}
            value={search}
            onChange={(event) => onSearch(event.currentTarget.value)}
          />
        </Box>
        {filters.map((filter) => (
          <Box key={filter.key} style={{ minWidth: 150 }}>
            <Text c="dimmed" fw={600} mb={4} size="xs" tt="uppercase" style={{ letterSpacing: '0.03em' }}>
              {filter.label}
            </Text>
            <NativeSelect
              aria-label={filter.label}
              data={filter.options}
              value={filter.value}
              onChange={(event) => filter.onChange(event.currentTarget.value)}
            />
          </Box>
        ))}
        <Box style={{ flex: 1 }} />
        <Group gap="xs" align="center">
          <Text c="dimmed" size="sm">
            Rows
          </Text>
          <Select
            aria-label="Rows per page"
            allowDeselect={false}
            data={['10', '25', '50', '100']}
            value={String(rows)}
            w={84}
            onChange={(value) => value && onRows(Number(value))}
          />
        </Group>
      </Group>

      <Group align="center" gap="xs" mt="sm" wrap="wrap" mih={26}>
        {applied.length > 0 ? (
          <>
            <Text c="dimmed" fw={500} size="sm">
              Filters
            </Text>
            {applied.map((filter) => (
              <Group
                key={filter.key}
                gap={6}
                wrap="nowrap"
                style={{
                  height: 26,
                  paddingLeft: 10,
                  paddingRight: 6,
                  background: 'var(--mw-primary-light)',
                  border: '1px solid var(--mw-border)',
                  borderRadius: 999,
                }}
              >
                <Text size="xs">
                  <Text component="span" c="dimmed">
                    {filter.label}:
                  </Text>{' '}
                  {filter.value}
                </Text>
                <ActionIcon
                  aria-label={`Clear ${filter.label}`}
                  color="gray"
                  size="xs"
                  variant="transparent"
                  onClick={() => filter.onChange(ALL)}
                >
                  <Icon name="x" size={13} />
                </ActionIcon>
              </Group>
            ))}
            <Text
              component="button"
              c="blue"
              fw={600}
              size="sm"
              style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '0 4px' }}
              onClick={() => applied.forEach((filter) => filter.onChange(ALL))}
            >
              Clear all
            </Text>
          </>
        ) : (
          <Text c="dimmed" size="sm">
            No filters applied
          </Text>
        )}
        <Box style={{ flex: 1 }} />
        {typeof count === 'number' && (
          <Text c="dimmed" size="sm">
            {typeof total === 'number' && total !== count
              ? `${count} of ${total} ${noun}`
              : `${count} ${noun}`}
          </Text>
        )}
      </Group>
    </Box>
  );
}
