import { ActionIcon, Group, Menu, Tooltip, type MantineColor } from '@mantine/core';
import type { ReactNode } from 'react';
import { DeleteIcon, DotsIcon, EditIcon, ExpandIcon, ViewIcon } from './IamIcons';

export type EntityAction = {
  label: 'View' | 'Edit' | 'Delete' | 'Remove' | 'Expand' | 'Collapse' | string;
  onClick: () => void;
  color?: MantineColor;
  disabled?: boolean;
  leftSection?: ReactNode;
  icon?: ReactNode;
};

type EntityActionMenuProps = {
  label: string;
  actions: EntityAction[];
};

export function EntityActionMenu({ label, actions }: EntityActionMenuProps) {
  return (
    <Menu position="bottom-end" shadow="md" withinPortal>
      <Menu.Target>
        <ActionIcon aria-label={label} size="sm" variant="subtle">
          <DotsIcon />
        </ActionIcon>
      </Menu.Target>
      <Menu.Dropdown>
        {actions.map((action) => (
          <Menu.Item
            key={action.label}
            color={action.color}
            disabled={action.disabled}
            leftSection={action.leftSection ?? action.icon ?? getActionIcon(action.label)}
            onClick={action.onClick}
          >
            {action.label}
          </Menu.Item>
        ))}
      </Menu.Dropdown>
    </Menu>
  );
}

type EntityActionGroupProps = {
  actions: EntityAction[];
};

export function EntityActionGroup({ actions }: EntityActionGroupProps) {
  return (
    <Group gap={4} justify="flex-end" wrap="nowrap">
      {actions.map((action) => (
        <Tooltip key={action.label} label={action.label} withArrow>
          <ActionIcon
            aria-label={action.label}
            color={action.color}
            disabled={action.disabled}
            size="sm"
            variant="subtle"
            onClick={action.onClick}
          >
            {action.icon ?? getActionIcon(action.label)}
          </ActionIcon>
        </Tooltip>
      ))}
    </Group>
  );
}

function getActionIcon(label: string) {
  const normalized = label.toLowerCase();

  if (normalized.includes('edit')) {
    return <EditIcon />;
  }

  if (normalized.includes('delete') || normalized.includes('remove') || normalized.includes('revoke')) {
    return <DeleteIcon />;
  }

  if (normalized.includes('expand') || normalized.includes('collapse')) {
    return <ExpandIcon />;
  }

  return <ViewIcon />;
}
