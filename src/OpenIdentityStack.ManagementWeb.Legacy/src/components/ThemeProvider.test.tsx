import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderManagementWeb } from '@/test/render';
import { ThemeToggle } from './ThemeToggle';

describe('ThemeProvider', () => {
  it('applies and persists the selected appearance mode', async () => {
    const user = userEvent.setup();
    renderManagementWeb(<ThemeToggle />);

    await user.click(screen.getByRole('button', { name: /appearance/i }));
    await user.click(screen.getByRole('menuitemradio', { name: /dark/i }));

    expect(localStorage.getItem('openidentitystack.management.theme')).toBe('dark');
    expect(document.documentElement).toHaveAttribute('data-mantine-color-scheme', 'dark');
  });
});
