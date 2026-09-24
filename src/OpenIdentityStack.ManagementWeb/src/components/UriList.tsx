import { ActionIcon, Box, Button, Group, Stack, Text, TextInput } from '@mantine/core';
import { useState } from 'react';
import { Icon } from './Icon';

type UriListRowState = { values: string[]; keys: number[]; nextKey: number };

function hasSameValues(left: string[], right: string[]): boolean {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

/** Repeating URI input list (add / remove rows). */
export function UriList({
  label,
  description,
  values,
  onChange,
  placeholder,
  error,
}: {
  label: string;
  description: string;
  values: string[];
  onChange: (values: string[]) => void;
  placeholder: string;
  error?: string;
}) {
  const [rowState, setRowState] = useState<UriListRowState>(() => ({
    values: [...values],
    keys: values.map((_, index) => index),
    nextKey: values.length,
  }));
  const rowStateMatchesValues = hasSameValues(rowState.values, values);
  const rowKeys = rowStateMatchesValues ? rowState.keys : values.map((_, index) => rowState.nextKey + index);
  const nextRowKey = rowState.nextKey + (rowStateMatchesValues ? 0 : values.length);

  const setAt = (index: number, value: string) => {
    const nextValues = values.map((item, i) => (i === index ? value : item));
    setRowState({ values: nextValues, keys: rowKeys, nextKey: nextRowKey });
    onChange(nextValues);
  };
  const removeAt = (index: number) => {
    if (values.length <= 1) {
      const nextValues = [''];
      setRowState({ values: nextValues, keys: rowKeys, nextKey: nextRowKey });
      onChange(nextValues);
      return;
    }

    const nextValues = values.filter((_, i) => i !== index);
    setRowState({
      values: nextValues,
      keys: rowKeys.filter((_, i) => i !== index),
      nextKey: nextRowKey,
    });
    onChange(nextValues);
  };
  const addRow = () => {
    const nextValues = [...values, ''];
    setRowState({ values: nextValues, keys: [...rowKeys, nextRowKey], nextKey: nextRowKey + 1 });
    onChange(nextValues);
  };

  return (
    <Box>
      <Text fw={600} size="sm">
        {label}
      </Text>
      <Text c="dimmed" size="xs" mb={6}>
        {description}
      </Text>
      <Stack gap="xs">
        {rowKeys.map((rowKey, index) => {
          const value = values[index] ?? '';

          return (
            <Group key={rowKey} gap="xs" wrap="nowrap">
              <TextInput
                style={{ flex: 1 }}
                styles={{ input: { fontFamily: 'var(--mw-mono)' } }}
                leftSection={<Icon name="link" size={15} />}
                placeholder={placeholder}
                value={value}
                onChange={(event) => setAt(index, event.currentTarget.value)}
              />
              <ActionIcon
                aria-label="Remove URI"
                color="gray"
                variant="default"
                size="lg"
                disabled={values.length <= 1 && !value.trim()}
                onClick={() => removeAt(index)}
              >
                <Icon name="trash-2" size={16} />
              </ActionIcon>
            </Group>
          );
        })}
      </Stack>
      {error && (
        <Text c="red" size="xs" mt={4}>
          {error}
        </Text>
      )}
      <Button size="xs" variant="subtle" leftSection={<Icon name="plus" size={14} />} mt={6} onClick={addRow}>
        Add URI
      </Button>
    </Box>
  );
}
