import { AppShell as MantineAppShell, Burger, Group, Text, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Outlet } from 'react-router';
import { ThemeToggle } from './ThemeToggle';
import { Navigation } from './Navigation';

export function AppShell() {
  const [opened, { toggle }] = useDisclosure();

  return (
    <MantineAppShell
      header={{ height: 64 }}
      navbar={{
        width: 280,
        breakpoint: 'sm',
        collapsed: { mobile: !opened },
      }}
      padding="md"
    >
      <MantineAppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Group>
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" />
            <div>
              <Title order={3}>OpenIdentityStack</Title>
              <Text size="xs" c="dimmed">
                Management Web
              </Text>
            </div>
          </Group>
          <ThemeToggle />
        </Group>
      </MantineAppShell.Header>
      <MantineAppShell.Navbar p="md">
        <Navigation />
      </MantineAppShell.Navbar>
      <MantineAppShell.Main>
        <Outlet />
      </MantineAppShell.Main>
    </MantineAppShell>
  );
}
