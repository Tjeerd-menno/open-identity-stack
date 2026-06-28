import { Button, Divider, Group, Stack, Text, Title } from '@mantine/core';
import type { ReactNode } from 'react';
import { ConfirmDialog } from './ConfirmDialog';

type FormSectionProps = {
  title: string;
  description?: string;
  children: ReactNode;
};

export function FormSection({ title, description, children }: FormSectionProps) {
  return (
    <Stack component="section" aria-label={title} gap="md">
      <div>
        <Title order={2}>{title}</Title>
        {description && <Text c="dimmed">{description}</Text>}
      </div>
      <Stack gap="sm">{children}</Stack>
    </Stack>
  );
}

type ActionBarProps = {
  submitLabel: string;
  cancelLabel?: string;
  isSubmitting?: boolean;
  onSubmit: () => void | Promise<void>;
  onCancel?: () => void;
  extraActions?: ReactNode;
};

export function ActionBar({
  submitLabel,
  cancelLabel = 'Cancel',
  isSubmitting = false,
  onSubmit,
  onCancel,
  extraActions,
}: ActionBarProps) {
  return (
    <Stack gap="md">
      <Divider />
      <Group justify="space-between">
        <Group>{extraActions}</Group>
        <Group justify="flex-end">
          {onCancel && (
            <Button variant="default" onClick={onCancel}>
              {cancelLabel}
            </Button>
          )}
          <Button loading={isSubmitting} disabled={isSubmitting} onClick={() => void onSubmit()}>
            {submitLabel}
          </Button>
        </Group>
      </Group>
    </Stack>
  );
}

type DestructiveActionDialogProps = {
  opened: boolean;
  onOpenChange: (opened: boolean) => void;
  onConfirm: () => void | Promise<void>;
  subject: string;
  message: string;
  loading?: boolean;
};

export function DestructiveActionDialog({
  opened,
  onOpenChange,
  onConfirm,
  subject,
  message,
  loading = false,
}: DestructiveActionDialogProps) {
  const label = `Delete ${subject}`;

  return (
    <ConfirmDialog
      opened={opened}
      onOpenChange={onOpenChange}
      onConfirm={onConfirm}
      title={label}
      message={message}
      confirmLabel={label}
      confirmColor="red"
      loading={loading}
    />
  );
}
