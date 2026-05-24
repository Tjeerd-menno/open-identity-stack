import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { LoadingSpinner } from './LoadingSpinner';

describe('LoadingSpinner', () => {
  it('renders the small size variant', () => {
    const { container } = render(<LoadingSpinner size="sm" />);

    expect(container.querySelector('svg')).toHaveClass('h-4', 'w-4');
  });

  it('renders the medium and large size variants', () => {
    const { container, rerender } = render(<LoadingSpinner size="md" />);

    expect(container.querySelector('svg')).toHaveClass('h-8', 'w-8');

    rerender(<LoadingSpinner size="lg" />);

    expect(container.querySelector('svg')).toHaveClass('h-12', 'w-12');
  });

  it('applies additional classes to the wrapper', () => {
    const { container } = render(<LoadingSpinner className="custom-class" />);

    expect(container.firstChild).toHaveClass('custom-class');
  });
});
