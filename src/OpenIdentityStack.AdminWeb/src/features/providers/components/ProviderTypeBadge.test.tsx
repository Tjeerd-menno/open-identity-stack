import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ProviderTypeBadge } from './ProviderTypeBadge';

describe('ProviderTypeBadge', () => {
  it('renders the OIDC provider type label', () => {
    render(<ProviderTypeBadge />);

    expect(screen.getByText('OIDC')).toBeInTheDocument();
  });
});
