import { Button, Paper, Stack, UnstyledButton, Group, Text } from '@mantine/core';
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
  const active = options.find((option) => option.value === preference) ?? options[2];
  const ActiveIcon = active.icon;

  return (
    <Stack gap={6} pos="relative">
      <UnstyledButton
        aria-label="Appearance"
        aria-expanded={opened}
        onClick={() => setOpened((value) => !value)}
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 12,
          width: '100%',
          padding: '9px 12px',
          borderRadius: 'var(--mantine-radius-md)',
          color: 'var(--mw-text-body)',
          fontSize: 14,
          fontWeight: 500,
        }}
        className="mw-sidebar-button"
      >
        <ActiveIcon />
        <Text size="sm" fw={500}>{active.label} appearance</Text>
      </UnstyledButton>
      {opened && (
        <Paper
          shadow="md"
          p="xs"
          radius="md"
          withBorder
          role="menu"
          pos="absolute"
          bottom="100%"
          left={0}
          right={0}
          mb={6}
          style={{ zIndex: 1000 }}
        >
          <Group gap="xs" grow>
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
