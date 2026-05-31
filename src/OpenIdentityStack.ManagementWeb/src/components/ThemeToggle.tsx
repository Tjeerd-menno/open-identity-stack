import { Button, Group, Paper, Stack } from '@mantine/core';
import { useState } from 'react';
import { useThemePreference } from './ThemeProvider';
import type { ThemePreference } from '@/lib/theme-preference';

const options: { value: ThemePreference; label: string }[] = [
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
  { value: 'system', label: 'System' },
];

export function ThemeToggle() {
  const { preference, setPreference } = useThemePreference();
  const [opened, setOpened] = useState(false);

  return (
    <Stack gap="xs" pos="relative">
      <Button variant="light" aria-label="Appearance" aria-expanded={opened} onClick={() => setOpened((value) => !value)}>
          Appearance: {options.find((option) => option.value === preference)?.label}
      </Button>
      {opened && (
        <Paper shadow="md" p="xs" radius="md" withBorder role="menu" pos="absolute" top="100%" right={0} style={{ zIndex: 1000 }}>
          <Group gap="xs">
        {options.map((option) => (
          <Button
            key={option.value}
            variant={preference === option.value ? 'filled' : 'subtle'}
            role="menuitemradio"
            aria-checked={preference === option.value}
            onClick={() => {
              setPreference(option.value);
              setOpened(false);
            }}
          >
            {option.label}
          </Button>
        ))}
          </Group>
        </Paper>
      )}
    </Stack>
  );
}
