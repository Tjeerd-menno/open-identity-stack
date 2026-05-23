import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ErrorBoundary } from './ErrorBoundary';

describe('ErrorBoundary', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('renders children when there is no error', () => {
    render(
      <ErrorBoundary>
        <div>Healthy child</div>
      </ErrorBoundary>
    );

    expect(screen.getByText('Healthy child')).toBeInTheDocument();
  });

  it('catches child errors and shows the fallback UI', () => {
    const Bomb = () => {
      throw new Error('Boom');
    };

    render(
      <ErrorBoundary>
        <Bomb />
      </ErrorBoundary>
    );

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    expect(screen.getByText('Boom')).toBeInTheDocument();
  });

  it('resets and re-renders children when Try again is clicked', async () => {
    const user = userEvent.setup();
    let shouldThrow = true;

    const FlakyChild = () => {
      if (shouldThrow) {
        throw new Error('Transient failure');
      }

      return <div>Recovered child</div>;
    };

    render(
      <ErrorBoundary>
        <FlakyChild />
      </ErrorBoundary>
    );

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();

    shouldThrow = false;
    await user.click(screen.getByRole('button', { name: /try again/i }));

    expect(screen.getByText('Recovered child')).toBeInTheDocument();
  });

  it('navigates home when Go to home is clicked', async () => {
    const user = userEvent.setup();

    const Bomb = () => {
      throw new Error('Boom');
    };

    vi.stubGlobal('location', { href: 'http://localhost/error-page' });

    render(
      <ErrorBoundary>
        <Bomb />
      </ErrorBoundary>
    );

    await user.click(screen.getByRole('button', { name: /go to home/i }));

    await waitFor(() => {
      expect(window.location.href).toBe('/');
    });
  });
});
