import { ActionIcon, AppShell as MantineAppShell, Badge, Burger, Group, Menu, Text, TextInput, Title } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { Outlet } from 'react-router';
import { useAuth } from '@/lib/auth-context';
import { LogoutIcon, SearchIcon } from './IamIcons';
import { ThemeToggle } from './ThemeToggle';
import { Navigation } from './Navigation';

export function AppShell() {
  const [opened, { toggle }] = useDisclosure();
  const auth = useAuth();

  return (
    <MantineAppShell
      header={{ height: 64 }}
      navbar={{
        width: 280,
        breakpoint: 'sm',
        collapsed: { mobile: !opened },
      }}
      padding="lg"
    >
      <MantineAppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Group gap="sm">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" />
            <div>
              <Title order={3} size="h4">OpenIdentityStack</Title>
              <Text size="xs" c="dimmed">
                IAM Console
              </Text>
            </div>
            <Badge visibleFrom="sm" color="teal" variant="light">Local tenant</Badge>
          </Group>
          <Group gap="xs" justify="flex-end">
            <TextInput
              aria-label="Global search"
              disabled
              leftSection={<SearchIcon />}
              placeholder="Search identities, apps, audit..."
              visibleFrom="md"
              w={320}
            />
            <ThemeToggle />
            <Menu position="bottom-end" shadow="md">
              <Menu.Target>
                <ActionIcon aria-label="Operator menu" variant="light">
                  {auth.displayName.slice(0, 1).toUpperCase()}
                </ActionIcon>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>{auth.displayName}</Menu.Label>
                <Menu.Item leftSection={<LogoutIcon />} onClick={() => void auth.logout()}>
                  Sign out
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </Group>
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
