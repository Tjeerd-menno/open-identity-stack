import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
  it('does not expose disabled global search and surfaces the operator in the sidebar footer', () => {
    renderManagementWeb(
      <AuthContextProvider value={auth}>
        <AppShell />
      </AuthContextProvider>
    );

    expect(screen.queryByRole('textbox', { name: /global search/i })).not.toBeInTheDocument();
    expect(screen.getByText('Test Operator')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign out/i })).toBeInTheDocument();
  });

  it('signs the operator out from the sidebar footer', async () => {
    const user = userEvent.setup();

    renderManagementWeb(
      <AuthContextProvider value={auth}>
        <AppShell />
      </AuthContextProvider>
    );

    await user.click(screen.getByRole('button', { name: /sign out/i }));

    expect(auth.logout).toHaveBeenCalledOnce();
  });

  it('shows breadcrumbs for nested management routes', () => {
    renderManagementWeb(
      <AuthContextProvider value={auth}>
        <AppShell />
      </AuthContextProvider>,
      { initialEntries: ['/providers/settings'] }
    );

    const breadcrumbs = screen.getByRole('navigation', { name: /breadcrumbs/i });

    expect(within(breadcrumbs).getByRole('link', { name: /overview/i })).toHaveAttribute('href', '/');
    expect(within(breadcrumbs).getByRole('link', { name: /identity providers/i })).toHaveAttribute('href', '/providers');
    expect(within(breadcrumbs).getByText(/authentication settings/i)).toBeInTheDocument();
  });

  it('closes mobile navigation after selecting a navigation item', async () => {
    const user = userEvent.setup();

    renderManagementWeb(
      <AuthContextProvider value={auth}>
        <AppShell />
      </AuthContextProvider>,
      { initialEntries: ['/applications'] }
    );

    const menuButton = screen.getByRole('button', { name: /navigation/i });

    await user.click(menuButton);
    expect(menuButton).toHaveAttribute('aria-expanded', 'true');

    await user.click(screen.getByRole('link', { name: /users/i }));

    expect(menuButton).toHaveAttribute('aria-expanded', 'false');
  });
});
