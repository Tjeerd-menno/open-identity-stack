import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SecretDisplay } from './SecretDisplay';

describe('SecretDisplay', () => {
  beforeEach(() => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('hides the secret by default and toggles visibility', async () => {
    const user = userEvent.setup();
    render(<SecretDisplay secret="super-secret" />);

    expect(screen.getByTestId('secret')).toHaveClass('blur-sm');

    await user.click(screen.getByTitle('Show secret'));

    expect(screen.getByTestId('secret')).not.toHaveClass('blur-sm');
    expect(screen.getByTitle('Hide secret')).toBeInTheDocument();
  });

  it('copies the secret and resets the copied message after the timeout', async () => {
    vi.useFakeTimers();
    render(<SecretDisplay secret="super-secret" />);

    await act(async () => {
      fireEvent.click(screen.getByTitle('Copy secret'));
      await Promise.resolve();
    });

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith('super-secret');
    expect(screen.getByText('✓ Copied to clipboard')).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(2000);
    });

    expect(screen.getByText('Click the copy icon to copy')).toBeInTheDocument();
  });
});
