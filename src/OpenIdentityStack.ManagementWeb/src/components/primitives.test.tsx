import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { MetaStrip, StatusBadge } from './primitives';

describe('StatusBadge', () => {
  it('renders the status text', () => {
    renderManagementWeb(<StatusBadge status="Active" />);
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('renders an arbitrary status without throwing', () => {
    renderManagementWeb(<StatusBadge status="PendingVerification" />);
    expect(screen.getByText('PendingVerification')).toBeInTheDocument();
  });
});

describe('MetaStrip', () => {
  it('renders labels and values', () => {
    renderManagementWeb(
      <MetaStrip
        items={[
          { label: 'Members', value: 3 },
          { label: 'Created', value: 'Today' },
        ]}
      />
    );
    expect(screen.getByText('Members')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('Created')).toBeInTheDocument();
    expect(screen.getByText('Today')).toBeInTheDocument();
  });
});
