import '@mantine/core/styles.css';
import '@mantine/notifications/styles.css';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import { ManagementThemeProvider } from '@/components/ThemeProvider';
import { AuthProvider } from '@/lib/auth';
import { AppRoutes } from '@/routes/AppRoutes';

const queryClient = new QueryClient();
const rootElement = document.getElementById('root');

if (!rootElement) {
  throw new Error('Root element was not found');
}

createRoot(rootElement).render(
  <StrictMode>
    <BrowserRouter>
      <QueryClientProvider client={queryClient}>
        <ManagementThemeProvider>
          <AuthProvider>
            <AppRoutes />
          </AuthProvider>
        </ManagementThemeProvider>
      </QueryClientProvider>
    </BrowserRouter>
  </StrictMode>
);
