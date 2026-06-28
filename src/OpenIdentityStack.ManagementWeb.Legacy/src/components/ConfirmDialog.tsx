import { Button, Group, Modal, Stack, Text } from '@mantine/core';

type ConfirmDialogProps = {
  opened: boolean;
  onOpenChange: (opened: boolean) => void;
  onConfirm: () => void | Promise<void>;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmColor?: string;
  loading?: boolean;
};

export function ConfirmDialog({
  opened,
  onOpenChange,
  onConfirm,
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  confirmColor = 'red',
  loading = false,
}: ConfirmDialogProps) {
  return (
    <Modal opened={opened} onClose={() => onOpenChange(false)} title={title} centered>
      <Stack gap="md">
        <Text size="sm">{message}</Text>
        <Group justify="flex-end">
          <Button variant="default" onClick={() => onOpenChange(false)}>
            {cancelLabel}
          </Button>
          <Button color={confirmColor} loading={loading} onClick={() => void onConfirm()}>
            {confirmLabel}
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
}
