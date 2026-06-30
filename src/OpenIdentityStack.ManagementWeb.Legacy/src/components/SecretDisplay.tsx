import { ActionIcon, Code, CopyButton, Group, Stack, Text, Tooltip } from '@mantine/core';
import { useState } from 'react';

type SecretDisplayProps = {
  label: string;
  value: string;
};

export function SecretDisplay({ label, value }: SecretDisplayProps) {
  const [visible, setVisible] = useState(false);

  return (
    <Stack gap={4}>
      <Text size="sm" fw={500}>
        {label}
      </Text>
      <Group gap="xs">
        <Code>{visible ? value : '••••••••••••••••'}</Code>
        <Tooltip label={visible ? `Hide ${label}` : `Show ${label}`}>
          <ActionIcon
            variant="subtle"
            aria-label={visible ? `Hide ${label}` : `Show ${label}`}
            onClick={() => setVisible((current) => !current)}
          >
            {visible ? 'Hide' : 'Show'}
          </ActionIcon>
        </Tooltip>
        <CopyButton value={value}>
          {({ copied, copy }) => (
            <Tooltip label={copied ? 'Copied' : `Copy ${label}`}>
              <ActionIcon variant="subtle" aria-label={`Copy ${label}`} onClick={copy}>
                {copied ? 'OK' : 'Copy'}
              </ActionIcon>
            </Tooltip>
          )}
        </CopyButton>
      </Group>
    </Stack>
  );
}
