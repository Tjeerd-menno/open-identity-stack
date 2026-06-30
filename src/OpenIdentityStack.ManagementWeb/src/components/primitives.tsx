import {
  Alert,
  Avatar,
  Badge,
  Box,
  Button,
  Card,
  Group,
  Loader,
  Stack,
  Text,
  ThemeIcon,
  Title,
  type BadgeProps,
  type MantineColor,
} from '@mantine/core';
import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { getInitials } from '@/lib/format';
import { Icon, type IconName } from './Icon';

/* ---------------- Page header ---------------- */
export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <Group align="flex-start" justify="space-between" gap="md" wrap="wrap" mb="lg">
      <Box>
        <Title order={1}>{title}</Title>
        {description && (
          <Text c="dimmed" mt={6} size="sm" maw={560}>
            {description}
          </Text>
        )}
      </Box>
      {actions && (
        <Group gap="sm" wrap="nowrap">
          {actions}
        </Group>
      )}
    </Group>
  );
}

/* ---------------- Stat tile ---------------- */
export function StatCard({
  label,
  value,
  delta,
  icon,
  color = 'blue',
}: {
  label: string;
  value: ReactNode;
  delta?: string;
  icon: IconName;
  color?: MantineColor;
}) {
  return (
    <Card padding="lg" style={{ flex: 1, minWidth: 0 }}>
      <Group align="flex-start" justify="space-between" wrap="nowrap">
        <Box style={{ minWidth: 0 }}>
          <Text c="dimmed" fw={500} size="sm">
            {label}
          </Text>
          <Title order={2} mt={6} style={{ letterSpacing: '-0.5px' }}>
            {value}
          </Title>
          {delta && (
            <Text c="dimmed" mt={4} size="xs">
              {delta}
            </Text>
          )}
        </Box>
        <ThemeIcon color={color} radius="md" size={40} variant="light">
          <Icon name={icon} size={20} />
        </ThemeIcon>
      </Group>
    </Card>
  );
}

/* ---------------- Section card (titled bordered block) ---------------- */
export function SectionCard({
  title,
  description,
  right,
  danger,
  children,
}: {
  title?: string;
  description?: string;
  right?: ReactNode;
  danger?: boolean;
  children: ReactNode;
}) {
  return (
    <Card padding="lg" style={danger ? { borderColor: 'var(--mantine-color-red-filled)' } : undefined}>
      {(title || right) && (
        <Group align="flex-start" justify="space-between" gap="md" mb={description ? 4 : 'md'}>
          {title && (
            <Text fw={700} size="md">
              {title}
            </Text>
          )}
          {right}
        </Group>
      )}
      {description && (
        <Text c="dimmed" mb="md" size="sm" maw={520}>
          {description}
        </Text>
      )}
      {children}
    </Card>
  );
}

/* ---------------- Definition row ---------------- */
export function FieldRow({ label, value, mono, last }: { label: string; value: ReactNode; mono?: boolean; last?: boolean }) {
  return (
    <Group
      align="center"
      justify="space-between"
      gap="lg"
      py="sm"
      style={{ borderBottom: last ? undefined : '1px solid var(--mw-border)' }}
      wrap="nowrap"
    >
      <Text c="dimmed" fw={500} size="sm" style={{ flexShrink: 0 }}>
        {label}
      </Text>
      <Text
        size="sm"
        ta="right"
        style={{ fontFamily: mono ? 'var(--mw-mono)' : undefined, wordBreak: 'break-all', minWidth: 0 }}
      >
        {value}
      </Text>
    </Group>
  );
}

/* ---------------- Status badge ---------------- */
const STATUS_COLORS: Record<string, MantineColor> = {
  active: 'green',
  enabled: 'green',
  available: 'green',
  verified: 'green',
  disabled: 'gray',
  pendingverification: 'yellow',
  pending: 'yellow',
  invited: 'blue',
  locked: 'orange',
  expired: 'yellow',
  loggedout: 'gray',
  revoked: 'red',
  error: 'red',
  failed: 'red',
};

export function StatusBadge({ status, color, ...rest }: { status: string; color?: MantineColor } & BadgeProps) {
  return (
    <Badge color={color ?? STATUS_COLORS[status.toLowerCase().replace(/\s+/g, '')] ?? 'gray'} variant="light" {...rest}>
      {status}
    </Badge>
  );
}

/* ---------------- Spinner ---------------- */
export function Spinner({ size = 'sm' }: { size?: number | string }) {
  return <Loader size={size} color="blue" />;
}

/* ---------------- Centered system states ---------------- */
export function CenteredState({
  icon,
  iconColor = 'gray',
  loading,
  title,
  text,
  actions,
  minHeight = 320,
}: {
  icon?: IconName;
  iconColor?: MantineColor;
  loading?: boolean;
  title: string;
  text?: string;
  actions?: ReactNode;
  minHeight?: number;
}) {
  return (
    <Card padding="xl" mih={minHeight} style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      <Stack align="center" gap="xs" ta="center">
        {loading ? (
          <Loader color="blue" size="lg" />
        ) : (
          <ThemeIcon color={iconColor} radius="md" size={52} variant="light">
            <Icon name={icon ?? 'circle-alert'} size={26} />
          </ThemeIcon>
        )}
        <Text fw={700} mt="xs" size="lg">
          {title}
        </Text>
        {text && (
          <Text c="dimmed" maw={400} size="sm">
            {text}
          </Text>
        )}
        {actions && (
          <Group gap="sm" mt="sm">
            {actions}
          </Group>
        )}
      </Stack>
    </Card>
  );
}

/* ---------------- Back link ---------------- */
export function BackLink({ label, to }: { label: string; to: string }) {
  return (
    <Button
      component={Link}
      to={to}
      variant="subtle"
      color="gray"
      size="sm"
      mb="sm"
      leftSection={<Icon name="arrow-left" size={15} />}
    >
      {label}
    </Button>
  );
}

/* ---------------- Detail header (entity icon + title + actions) ---------------- */
export function DetailHeader({
  icon,
  avatarName,
  color = 'blue',
  title,
  description,
  badge,
  actions,
}: {
  icon?: IconName;
  avatarName?: string;
  color?: MantineColor;
  title: string;
  description?: string;
  badge?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <Group align="flex-start" justify="space-between" gap="md" mb="lg" wrap="wrap">
      <Group gap="md" wrap="nowrap">
        {avatarName ? (
          <Avatar color={color} name={avatarName} radius="xl" size={52}>
            {getInitials(avatarName)}
          </Avatar>
        ) : (
          <ThemeIcon color={color} radius="md" size={52} variant="light">
            <Icon name={icon ?? 'box'} size={26} />
          </ThemeIcon>
        )}
        <Box>
          <Group gap="sm">
            <Title order={1}>{title}</Title>
            {badge}
          </Group>
          {description && (
            <Text c="dimmed" mt={4} size="sm">
              {description}
            </Text>
          )}
        </Box>
      </Group>
      {actions && (
        <Group gap="sm" wrap="nowrap">
          {actions}
        </Group>
      )}
    </Group>
  );
}

/* ---------------- Meta strip (uppercase label + bold value) ---------------- */
export function MetaStrip({ items }: { items: Array<{ label: string; value: ReactNode }> }) {
  return (
    <Group gap={40} mb="lg" wrap="wrap">
      {items.map((item) => (
        <Box key={item.label}>
          <Text c="dimmed" fw={500} size="xs" tt="uppercase" style={{ letterSpacing: '0.04em' }}>
            {item.label}
          </Text>
          <Text fw={700} mt={3} size="lg">
            {item.value}
          </Text>
        </Box>
      ))}
    </Group>
  );
}

export function ErrorState({ title = 'Something went wrong', message }: { title?: string; message: string }) {
  return (
    <Alert color="red" icon={<Icon name="circle-alert" size={18} />} title={title} variant="light">
      {message}
    </Alert>
  );
}
