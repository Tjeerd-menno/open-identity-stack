import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mockUseUsers = vi.fn();
const mockUseRoles = vi.fn();
const mockUseGroups = vi.fn();
const mockUseSessions = vi.fn();
const mockUseAuth = vi.fn();

vi.mock('@/features/users', () => ({
  useUsers: (options: { enabled: boolean }) => mockUseUsers(options),
}));

vi.mock('@/features/roles', () => ({
  useRoles: (options: { enabled: boolean }) => mockUseRoles(options),
}));

vi.mock('@/features/groups', () => ({
  useGroups: (options: { enabled: boolean }) => mockUseGroups(options),
}));

vi.mock('@/features/sessions', () => ({
  useSessions: (options: { enabled: boolean }) => mockUseSessions(options),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

import { Dashboard } from './Dashboard';

describe('Dashboard', () => {
  beforeEach(() => {
    mockUseAuth.mockReset();
    mockUseUsers.mockReset();
    mockUseRoles.mockReset();
    mockUseGroups.mockReset();
    mockUseSessions.mockReset();

    mockUseAuth.mockReturnValue({ isAuthenticated: true, isLoading: false });
    mockUseUsers.mockReturnValue({ data: { totalCount: 10 } });
    mockUseRoles.mockReturnValue({ data: { totalCount: 4 } });
    mockUseGroups.mockReturnValue({ data: { totalCount: 3 } });
    mockUseSessions.mockReturnValue({
      data: {
        items: [{ status: 'Active' }, { status: 'Revoked' }, { status: 'Active' }],
      },
    });

    vi.spyOn(console, 'log').mockImplementation(() => undefined);
  });

  it('renders aggregated stats and quick action links', () => {
    render(<Dashboard />);

    expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
    expect(screen.getByText('10')).toBeInTheDocument();
    expect(screen.getByText('4')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();

    expect(screen.getByRole('link', { name: '• Create new user' })).toHaveAttribute('href', '/users/create');
    expect(screen.getByRole('link', { name: '• View active sessions' })).toHaveAttribute('href', '/sessions');
  });

  it('passes auth-dependent enabled flags to data hooks and falls back to zero values', () => {
    mockUseAuth.mockReturnValue({ isAuthenticated: false, isLoading: false });
    mockUseUsers.mockReturnValue({ data: undefined });
    mockUseRoles.mockReturnValue({ data: undefined });
    mockUseGroups.mockReturnValue({ data: undefined });
    mockUseSessions.mockReturnValue({ data: undefined });

    render(<Dashboard />);

    expect(mockUseUsers).toHaveBeenCalledWith({ enabled: false });
    expect(mockUseRoles).toHaveBeenCalledWith({ enabled: false });
    expect(mockUseGroups).toHaveBeenCalledWith({ enabled: false });
    expect(mockUseSessions).toHaveBeenCalledWith({ enabled: false });

    const zeros = screen.getAllByText('0');
    expect(zeros).toHaveLength(4);
  });
});
