import { Button, Paper, Stack, type MantineColor } from '@mantine/core';
import { useState } from 'react';
import type { ReactNode } from 'react';
import { DotsIcon } from './IamIcons';

export type EntityAction = {
  label: string;
  onClick: () => void;
  color?: MantineColor;
  disabled?: boolean;
  leftSection?: ReactNode;
};

type EntityActionMenuProps = {
  label: string;
  actions: EntityAction[];
};

export function EntityActionMenu({ label, actions }: EntityActionMenuProps) {
  const [opened, setOpened] = useState(false);

  return (
    <div style={{ display: 'inline-flex', position: 'relative' }}>
      <button
        type="button"
        aria-expanded={opened}
        aria-haspopup="menu"
        aria-label={label}
        onClick={() => setOpened((value) => !value)}
        style={{
          alignItems: 'center',
          background: 'transparent',
          border: 0,
          borderRadius: 6,
          color: 'var(--mantine-color-dimmed)',
          cursor: 'pointer',
          display: 'inline-flex',
          height: 28,
          justifyContent: 'center',
          padding: 0,
          width: 28,
        }}
      >
        <DotsIcon />
      </button>
      {opened && (
        <Paper
          p={4}
          role="menu"
          shadow="md"
          style={{ minWidth: 150, position: 'absolute', right: 0, top: '100%', zIndex: 20 }}
          withBorder
        >
          <Stack gap={2}>
            {actions.map((action) => (
              <Button
                key={action.label}
                color={action.color}
                disabled={action.disabled}
                leftSection={action.leftSection}
                role="menuitem"
                size="xs"
                style={{ justifyContent: 'flex-start' }}
                variant="subtle"
                onClick={() => {
                  setOpened(false);
                  action.onClick();
                }}
              >
                {action.label}
              </Button>
            ))}
          </Stack>
        </Paper>
      )}
    </div>
  );
}
