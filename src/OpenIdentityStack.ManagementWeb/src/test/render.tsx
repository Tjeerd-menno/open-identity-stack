import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router';
import { ManagementThemeProvider } from '@/components/ThemeProvider';
import { AuthContextProvider, type AuthContextValue } from '@/lib/auth-context';

export function makeAuth(overrides: Partial<AuthContextValue> = {}): AuthContextValue {
  return {
    isAuthenticated: true,
    isLoading: false,
    displayName: 'Test Operator',
    permissions: ['*'],
    login: async () => {},
    logout: async () => {},
    getAccessToken: async () => 'token',
    ...overrides,
  };
}

type RenderOptions = {
  initialEntries?: string[];
  auth?: AuthContextValue;
};

export function renderManagementWeb(ui: ReactElement, options: RenderOptions = {}) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  const tree = options.auth ? <AuthContextProvider value={options.auth}>{ui}</AuthContextProvider> : ui;

  return render(
    <MemoryRouter initialEntries={options.initialEntries}>
      <QueryClientProvider client={queryClient}>
        <ManagementThemeProvider>{tree}</ManagementThemeProvider>
      </QueryClientProvider>
    </MemoryRouter>
  );
}
