import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApplicationPermissionDiagnosticsPanel } from './ApplicationPermissionDiagnosticsPanel';

const hooks = vi.hoisted(() => ({
  useApplicationPermissionDiagnostics: vi.fn(),
}));

vi.mock('../hooks', () => hooks);

describe('ApplicationPermissionDiagnosticsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does not render when there are no diagnostics issues', () => {
    hooks.useApplicationPermissionDiagnostics.mockReturnValue({
      data: { issues: [] },
      isFetching: false,
      refetch: vi.fn(),
    });

    render(<ApplicationPermissionDiagnosticsPanel />);

    expect(screen.queryByText('Assignment Diagnostics')).not.toBeInTheDocument();
    expect(screen.queryByText('No integrity issues detected.')).not.toBeInTheDocument();
  });

  it('renders when diagnostics issues exist', () => {
    hooks.useApplicationPermissionDiagnostics.mockReturnValue({
      data: {
        issues: [
          {
            assignmentStore: 'role',
            assignmentId: 'role-1',
            assignmentDisplayName: 'Application Admin',
            kind: 'malformed',
            permission: 'applications:*',
            message: 'Permission string is malformed.',
          },
        ],
      },
      isFetching: false,
      refetch: vi.fn(),
    });

    render(<ApplicationPermissionDiagnosticsPanel />);

    expect(screen.getByText('Assignment Diagnostics')).toBeInTheDocument();
    expect(screen.getByText('1 permission assignment issue need review.')).toBeInTheDocument();
    expect(screen.getByText('applications:*')).toBeInTheDocument();
  });
});
