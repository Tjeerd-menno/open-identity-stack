import { Anchor, Box, Group, Paper, Stack, Text, UnstyledButton } from '@mantine/core';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { ArrowLeftIcon } from './IamIcons';

type BackLinkProps = {
  label: string;
  to?: string;
  onClick?: () => void;
};

/** Muted "← Back to X" affordance shown above a detail-view header. */
export function BackLink({ label, to, onClick }: BackLinkProps) {
  const content = (
    <Group gap={6} wrap="nowrap">
      <ArrowLeftIcon />
      <Text size="sm" fw={500}>{label}</Text>
    </Group>
  );

  if (to) {
    return (
      <Anchor component={Link} to={to} c="dimmed" underline="never" w="fit-content" mb={4}>
        {content}
      </Anchor>
    );
  }

  return (
    <UnstyledButton c="dimmed" onClick={onClick} mb={4} style={{ width: 'fit-content' }}>
      {content}
    </UnstyledButton>
  );
}

type MetaItem = {
  label: string;
  value: ReactNode;
};

/** Horizontal strip of small-caps label / strong value pairs beneath a header. */
export function MetaStrip({ items }: { items: MetaItem[] }) {
  return (
    <Group gap={40} wrap="wrap">
      {items.map((item) => (
        <Stack key={item.label} gap={2}>
          <Text size="xs" c="dimmed" fw={500} tt="uppercase" style={{ letterSpacing: '0.04em' }}>
            {item.label}
          </Text>
          <Text fw={700} style={{ fontSize: 16, color: 'var(--mw-text-strong)' }}>
            {item.value}
          </Text>
        </Stack>
      ))}
    </Group>
  );
}

type FieldRowProps = {
  label: string;
  value: ReactNode;
  mono?: boolean;
  last?: boolean;
};

/** Label / value row with a hairline separator, for detail field lists. */
export function FieldRow({ label, value, mono, last }: FieldRowProps) {
  return (
    <Group
      justify="space-between"
      align="center"
      gap="lg"
      wrap="nowrap"
      style={{
        padding: '13px 0',
        borderBottom: last ? undefined : '1px solid var(--mw-border)',
      }}
    >
      <Text size="sm" c="dimmed" fw={500} style={{ flexShrink: 0 }}>{label}</Text>
      <Box
        style={{
          textAlign: 'right',
          minWidth: 0,
          wordBreak: 'break-all',
          fontFamily: mono ? 'var(--mw-font-mono)' : undefined,
          fontSize: mono ? 13 : undefined,
          color: 'var(--mw-text-body)',
        }}
      >
        {value}
      </Box>
    </Group>
  );
}

type DetailCardProps = {
  title: string;
  description?: string;
  children: ReactNode;
};

/** Bordered card with a title (+ optional description) wrapping a field list. */
export function DetailCard({ title, description, children }: DetailCardProps) {
  return (
    <Paper withBorder radius="sm" p="lg">
      <Text fw={700} style={{ color: 'var(--mw-text-strong)' }}>{title}</Text>
      {description && <Text size="sm" c="dimmed" mt={2}>{description}</Text>}
      <Box mt="sm">{children}</Box>
    </Paper>
  );
}
