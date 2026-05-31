import { Alert, Stack, Text, Title } from '@mantine/core';

type PlaceholderPageProps = {
  title: string;
};

export function PlaceholderPage({ title }: PlaceholderPageProps) {
  return (
    <Stack>
      <Title order={1}>{title}</Title>
      <Alert color="blue">
        <Text>This Management Web domain is reserved for a future vertical slice.</Text>
      </Alert>
    </Stack>
  );
}
