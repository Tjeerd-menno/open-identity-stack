import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router';
import { ManagementThemeProvider } from '@/components/ThemeProvider';

type RenderManagementWebOptions = {
  initialEntries?: string[];
};

export function renderManagementWeb(ui: ReactElement, options: RenderManagementWebOptions = {}) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return render(
    <MemoryRouter initialEntries={options.initialEntries}>
      <QueryClientProvider client={queryClient}>
        <ManagementThemeProvider>{ui}</ManagementThemeProvider>
      </QueryClientProvider>
    </MemoryRouter>
  );
}
