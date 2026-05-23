import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { RoleTypeBadge } from './roles/components/RoleTypeBadge';
import { ServiceAccountStatusBadge } from './service-accounts/components/ServiceAccountStatusBadge';
import { SessionStatusBadge } from './sessions/components/SessionStatusBadge';
import { UserStatusBadge } from './users/components/UserStatusBadge';

describe('status badge components', () => {
  it.each([
    ['Active', 'Active'],
    ['PendingVerification', 'Pending'],
    ['Disabled', 'Disabled'],
    ['Locked', 'Locked'],
  ] as const)('renders user status %s as %s', (status, label) => {
    render(<UserStatusBadge status={status} />);

    expect(screen.getByText(label)).toHaveAttribute('data-status', status);
  });

  it.each([
    ['Active', 'Active'],
    ['Expired', 'Expired'],
    ['Revoked', 'Revoked'],
    ['LoggedOut', 'Logged Out'],
  ] as const)('renders session status %s as %s', (status, label) => {
    render(<SessionStatusBadge status={status} />);

    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it.each(['Active', 'Disabled'] as const)('renders service account status %s', (status) => {
    render(<ServiceAccountStatusBadge status={status} />);

    expect(screen.getByText(status)).toHaveAttribute('data-status', status);
  });

  it('distinguishes system and custom roles', () => {
    const { rerender } = render(
      <RoleTypeBadge
        role={{
          id: 'role-1',
          name: 'admin',
          displayName: 'Admin',
          isSystemRole: true,
          isActive: true,
        }}
      />
    );

    expect(screen.getByText('System')).toHaveAttribute('data-system-role', 'true');

    rerender(
      <RoleTypeBadge
        role={{
          id: 'role-2',
          name: 'auditor',
          displayName: 'Auditor',
          isSystemRole: false,
          isActive: true,
        }}
      />
    );

    expect(screen.getByText('Custom')).toBeInTheDocument();
  });
});
