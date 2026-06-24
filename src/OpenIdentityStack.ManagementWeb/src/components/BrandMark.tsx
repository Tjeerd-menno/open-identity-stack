import { Box, Group, Text } from '@mantine/core';
import { RolesIcon } from './IamIcons';

type BrandMarkProps = {
  /** Size of the mark square in pixels. */
  size?: number;
  /** Wordmark font size in pixels. */
  fontSize?: number;
};

/**
 * OpenIdentityStack brand lockup: a primary-filled rounded square holding the
 * shield-check glyph, followed by the "OpenIdentity" + "Stack" wordmark where
 * "Stack" is rendered in the link colour. Used in the sidebar header and any
 * other branded surface in Management Web.
 */
export function BrandMark({ size = 30, fontSize = 15.5 }: BrandMarkProps) {
  return (
    <Group gap={10} wrap="nowrap" align="center">
      <Box
        style={{
          width: size,
          height: size,
          borderRadius: size * 0.27,
          background: 'var(--mw-primary)',
          color: 'var(--mantine-color-white)',
          display: 'inline-flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexShrink: 0,
        }}
      >
        <RolesIcon style={{ width: size * 0.6, height: size * 0.6 }} />
      </Box>
      <Text
        fw={700}
        style={{ fontSize, letterSpacing: '-0.2px', color: 'var(--mw-text-strong)', whiteSpace: 'nowrap' }}
      >
        OpenIdentity<span style={{ color: 'var(--mw-text-link)' }}>Stack</span>
      </Text>
    </Group>
  );
}
