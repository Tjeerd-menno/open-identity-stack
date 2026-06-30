import { ActionIcon, Menu } from '@mantine/core';
import { Icon, type IconName } from './Icon';

export type RowMenuItem =
  | { separator: true }
  | {
      separator?: false;
      label: string;
      icon: IconName;
      onClick?: () => void;
      danger?: boolean;
      disabled?: boolean;
    };

/** The standard per-row overflow ("kebab") menu used across every list table. */
export function RowMenu({ items, label = 'Row actions' }: { items: RowMenuItem[]; label?: string }) {
  return (
    <Menu position="bottom-end" shadow="md" width={196} withinPortal>
      <Menu.Target>
        <ActionIcon
          aria-label={label}
          color="gray"
          variant="subtle"
          onClick={(event) => event.stopPropagation()}
        >
          <Icon name="ellipsis-vertical" size={16} />
        </ActionIcon>
      </Menu.Target>
      <Menu.Dropdown onClick={(event) => event.stopPropagation()}>
        {items.map((item, index) =>
          'separator' in item && item.separator ? (
            <Menu.Divider key={`sep-${index}`} />
          ) : (
            <Menu.Item
              key={item.label}
              color={item.danger ? 'red' : undefined}
              disabled={item.disabled}
              leftSection={<Icon name={item.icon} size={15} />}
              onClick={(event) => {
                event.stopPropagation();
                item.onClick?.();
              }}
            >
              {item.label}
            </Menu.Item>
          )
        )}
      </Menu.Dropdown>
    </Menu>
  );
}
