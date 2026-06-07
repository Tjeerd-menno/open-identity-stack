import { screen } from '@testing-library/react';
import { AuthContextProvider, type AuthContextValue } from '@/lib/auth-context';
import { renderManagementWeb } from '@/test/render';
import { AppShell } from './AppShell';

const auth: AuthContextValue = {
  isAuthenticated: true,
  isLoading: false,
  displayName: 'Test Operator',
  permissions: ['*'],
  login: vi.fn(),
  logout: vi.fn(),
  getAccessToken: vi.fn(async () => 'token'),
};

describe('AppShell', () => {
  it('does not expose disabled global search in the header', () => {
    renderManagementWeb(
      <AuthContextProvider value={auth}>
        <AppShell />
      </AuthContextProvider>
    );

    expect(screen.queryByRole('textbox', { name: /global search/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /operator menu/i })).toBeInTheDocument();
  });
});
