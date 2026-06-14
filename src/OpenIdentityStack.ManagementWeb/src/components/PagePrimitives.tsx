import {
  Badge,
  Button,
  Center,
  Group,
  Loader,
  MultiSelect,
  NativeSelect,
  Paper,
  Select,
  Stack,
  Text,
  TextInput,
  Title,
  type MantineColor,
} from '@mantine/core';
import type { ReactNode } from 'react';
import { ChevronDownIcon, CloseIcon, SearchIcon } from './IamIcons';

type PageHeaderProps = {
  title: string;
  description?: string;
  badges?: Array<{ label: string; color?: MantineColor; variant?: 'light' | 'filled' | 'outline' }>;
  actions?: ReactNode;
  metadata?: ReactNode;
};

export function PageHeader({ title, description, badges = [], actions, metadata }: PageHeaderProps) {
  return (
    <Group justify="space-between" align="flex-start" gap="lg">
      <Stack gap={6}>
        <Group gap="xs" align="center">
          <Title order={1}>{title}</Title>
          {badges.map((badge) => (
            <Badge key={badge.label} color={badge.color ?? 'blue'} variant={badge.variant ?? 'light'}>
              {badge.label}
            </Badge>
          ))}
        </Group>
        {description && <Text c="dimmed">{description}</Text>}
        {metadata}
      </Stack>
      {actions && <Group gap="xs">{actions}</Group>}
    </Group>
  );
}

export type AppliedFilter = {
  key: string;
  label: string;
  value: string;
  onRemove?: () => void;
};

type ToolbarSelectFilter = {
  key: string;
  label: string;
  value: string | string[] | null;
  data: Array<{ value: string; label: string }> | string[];
  onChange: (value: string | string[] | null) => void;
  multiple?: boolean;
};

type PageToolbarProps = {
  searchLabel?: string;
  searchPlaceholder?: string;
  searchValue?: string;
  onSearchChange?: (value: string) => void;
  filters?: ToolbarSelectFilter[];
  appliedFilters?: AppliedFilter[];
  resultCount?: number;
  pageSize?: number;
  pageSizeOptions?: number[];
  onPageSizeChange?: (pageSize: number) => void;
  onApply?: () => void;
  onClear?: () => void;
  actions?: ReactNode;
  secondaryFields?: ReactNode;
};

export function PageToolbar({
  searchLabel = 'Search',
  searchPlaceholder = 'Search...',
  searchValue,
  onSearchChange,
  filters = [],
  appliedFilters = [],
  resultCount,
  pageSize,
  pageSizeOptions = [10, 20, 50, 100],
  onPageSizeChange,
  onApply,
  onClear,
  actions,
  secondaryFields,
}: PageToolbarProps) {
  const hasAppliedFilters = appliedFilters.length > 0;

  return (
    <Paper withBorder radius="md" p="md">
      <Stack gap="sm">
        <Group align="flex-end" gap="sm">
          {onSearchChange && (
            <TextInput
              aria-label={searchLabel}
              label={searchLabel}
              leftSection={<SearchIcon aria-hidden="true" />}
              maw={460}
              placeholder={searchPlaceholder}
              style={{ flex: '1 1 280px' }}
              value={searchValue ?? ''}
              onChange={(event) => onSearchChange(event.currentTarget.value)}
            />
          )}
          {filters.map((filter) => (
            filter.multiple ? (
              <MultiSelect
                key={filter.key}
                clearable
                data={filter.data}
                label={filter.label}
                maw={280}
                rightSection={<ChevronDownIcon />}
                rightSectionProps={{ 'aria-hidden': true }}
                searchable
                value={Array.isArray(filter.value) ? filter.value : []}
                onChange={(value) => filter.onChange(value)}
              />
            ) : (
              <NativeSelect
                key={filter.key}
                data={[
                  { value: '', label: `All ${filter.label.toLowerCase()}` },
                  ...filter.data.map((item) => (typeof item === 'string' ? { value: item, label: item } : item)),
                ]}
                label={filter.label}
                maw={240}
                rightSection={<ChevronDownIcon />}
                rightSectionProps={{ 'aria-hidden': true }}
                value={typeof filter.value === 'string' ? filter.value : ''}
                onChange={(event) => filter.onChange(event.currentTarget.value || null)}
              />
            )
          ))}
          {onPageSizeChange && pageSize && (
            <Select
              allowDeselect={false}
              data={pageSizeOptions.map((option) => ({ value: String(option), label: `${option} / page` }))}
              label="Rows"
              maw={150}
              rightSection={<ChevronDownIcon />}
              rightSectionProps={{ 'aria-hidden': true }}
              value={String(pageSize)}
              onChange={(value) => value && onPageSizeChange(Number(value))}
            />
          )}
          {(onApply || onClear || actions) && (
            <Group gap="xs">
              {onApply && <Button onClick={onApply}>Apply filters</Button>}
              {onClear && <Button variant="default" onClick={onClear}>Clear filters</Button>}
              {actions}
            </Group>
          )}
        </Group>
        <Group justify="space-between" gap="xs">
          <AppliedFilters filters={appliedFilters} />
          {typeof resultCount === 'number' && (
            <Text size="sm" c="dimmed">
              {resultCount.toLocaleString()} result{resultCount === 1 ? '' : 's'}
            </Text>
          )}
          {!hasAppliedFilters && typeof resultCount !== 'number' && <span />}
        </Group>
        {secondaryFields}
      </Stack>
    </Paper>
  );
}

export function AppliedFilters({ filters }: { filters: AppliedFilter[] }) {
  if (filters.length === 0) {
    return <Text size="sm" c="dimmed">No filters applied</Text>;
  }

  return (
    <Group gap={6}>
      {filters.map((filter) => (
        <Badge
          key={filter.key}
          color="blue"
          rightSection={filter.onRemove ? (
            <button
              aria-label={`Remove ${filter.label} filter`}
              onClick={filter.onRemove}
              style={{
                all: 'unset',
                cursor: 'pointer',
                display: 'inline-flex',
                lineHeight: 1,
                marginLeft: 4,
              }}
              type="button"
            >
              <CloseIcon />
            </button>
          ) : null}
          variant="light"
        >
          {filter.label}: {filter.value}
        </Badge>
      ))}
    </Group>
  );
}

type LoadingStateProps = {
  title: string;
  description: string;
  label?: string;
};

export function LoadingState({ title, description, label = title }: LoadingStateProps) {
  return (
    <Center
      aria-label={label}
      mih={320}
      role="status"
    >
      <Paper withBorder radius="md" p="xl" maw={520} w="100%">
        <Stack align="center" gap="sm" ta="center">
          <Loader aria-hidden="true" />
          <Title order={1} size="h3">{title}</Title>
          <Text c="dimmed" size="sm">{description}</Text>
        </Stack>
      </Paper>
    </Center>
  );
}
