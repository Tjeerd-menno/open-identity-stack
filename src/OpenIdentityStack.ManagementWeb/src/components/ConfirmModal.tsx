import { Button, Group, Modal, Stack, Text, ThemeIcon } from '@mantine/core';
import { Icon } from './Icon';

export function ConfirmModal({
  opened,
  title,
  message,
  confirmLabel = 'Confirm',
  danger = true,
  loading = false,
  disabled = false,
  onConfirm,
  onClose,
}: {
  opened: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  danger?: boolean;
  loading?: boolean;
  disabled?: boolean;
  onConfirm: () => void;
  onClose: () => void;
}) {
  return (
    <Modal opened={opened} onClose={onClose} withCloseButton={false} centered size={440}>
      <Group align="flex-start" gap="md" wrap="nowrap">
        <ThemeIcon color={danger ? 'red' : 'orange'} radius="md" size={40} variant="light" style={{ flexShrink: 0 }}>
          <Icon name="triangle-alert" size={20} />
        </ThemeIcon>
        <Stack gap={4}>
          <Text fw={700} size="md">
            {title}
          </Text>
          <Text c="dimmed" size="sm">
            {message}
          </Text>
        </Stack>
      </Group>
      <Group justify="flex-end" gap="sm" mt="lg">
        <Button variant="default" onClick={onClose} disabled={loading}>
          Cancel
        </Button>
        <Button color={danger ? 'red' : undefined} loading={loading} disabled={disabled} onClick={onConfirm}>
          {confirmLabel}
        </Button>
      </Group>
    </Modal>
  );
}
