import { ActionIcon, Button, Group, Paper, Stack } from '@mantine/core';
import { useState } from 'react';
import { useThemePreference } from './ThemeProvider';
import { MoonIcon, SunIcon, SystemThemeIcon } from './IamIcons';
import type { ThemePreference } from '@/lib/theme-preference';

const options: { value: ThemePreference; label: string; icon: typeof SunIcon }[] = [
  { value: 'light', label: 'Light', icon: SunIcon },
  { value: 'dark', label: 'Dark', icon: MoonIcon },
  { value: 'system', label: 'System', icon: SystemThemeIcon },
];

export function ThemeToggle() {
  const { preference, setPreference } = useThemePreference();
  const [opened, setOpened] = useState(false);
  const ActiveIcon = options.find((option) => option.value === preference)?.icon ?? SystemThemeIcon;

  return (
    <Stack gap="xs" pos="relative">
      <ActionIcon variant="light" aria-label="Appearance" aria-expanded={opened} onClick={() => setOpened((value) => !value)}>
        <ActiveIcon />
      </ActionIcon>
      {opened && (
        <Paper shadow="md" p="xs" radius="md" withBorder role="menu" pos="absolute" top="100%" right={0} style={{ zIndex: 1000 }}>
          <Group gap="xs">
            {options.map((option) => {
              const Icon = option.icon;

              return (
                <Button
                  key={option.value}
                  leftSection={<Icon />}
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
              );
            })}
          </Group>
        </Paper>
      )}
    </Stack>
  );
}
