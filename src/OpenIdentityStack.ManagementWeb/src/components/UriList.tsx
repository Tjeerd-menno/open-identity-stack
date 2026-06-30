import { ActionIcon, Box, Button, Group, Stack, Text, TextInput } from '@mantine/core';
import { Icon } from './Icon';

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
  const setAt = (index: number, value: string) => onChange(values.map((item, i) => (i === index ? value : item)));
  const removeAt = (index: number) => onChange(values.length <= 1 ? [''] : values.filter((_, i) => i !== index));

  return (
    <Box>
      <Text fw={600} size="sm">
        {label}
      </Text>
      <Text c="dimmed" size="xs" mb={6}>
        {description}
      </Text>
      <Stack gap="xs">
        {values.map((value, index) => (
          <Group key={index} gap="xs" wrap="nowrap">
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
        ))}
      </Stack>
      {error && (
        <Text c="red" size="xs" mt={4}>
          {error}
        </Text>
      )}
      <Button size="xs" variant="subtle" leftSection={<Icon name="plus" size={14} />} mt={6} onClick={() => onChange([...values, ''])}>
        Add URI
      </Button>
    </Box>
  );
}
