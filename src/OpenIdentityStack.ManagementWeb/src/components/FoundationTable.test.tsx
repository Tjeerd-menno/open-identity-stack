import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { renderManagementWeb } from '@/test/render';
import { cleanup } from '@testing-library/react';
import { FoundationTable } from './FoundationTable';

type Row = {
  id: string;
  name: string;
  status: string;
};

const columns = [
  { header: 'Name', accessorKey: 'name' as const },
  { header: 'Status', accessorKey: 'status' as const },
];

describe('FoundationTable', () => {
  it('renders rows and pagination metadata', () => {
    renderManagementWeb(
      <FoundationTable<Row>
        columns={columns}
        data={[{ id: '1', name: 'Alice', status: 'Active' }]}
        pagination={{ page: 2, pageSize: 20, totalCount: 45, totalPages: 3, onPageChange: vi.fn() }}
      />
    );

    expect(screen.getByRole('cell', { name: 'Alice' })).toBeInTheDocument();
    expect(screen.getByText('Showing 21 to 40 of 45 results')).toBeInTheDocument();
  });

  it('shows empty and loading states', () => {
    renderManagementWeb(<FoundationTable<Row> columns={columns} data={[]} emptyMessage="No records" />);

    expect(screen.getByText('No records')).toBeInTheDocument();

    cleanup();
    renderManagementWeb(<FoundationTable<Row> columns={columns} data={[]} isLoading />);

    expect(screen.getByLabelText('Loading table data')).toBeInTheDocument();
  });

  it('calls page changes from pagination controls', async () => {
    const onPageChange = vi.fn();
    renderManagementWeb(
      <FoundationTable<Row>
        columns={columns}
        data={[{ id: '1', name: 'Alice', status: 'Active' }]}
        pagination={{ page: 1, pageSize: 20, totalCount: 45, totalPages: 3, onPageChange }}
      />
    );

    await userEvent.click(screen.getByRole('button', { name: /next/i }));

    expect(onPageChange).toHaveBeenCalledWith(2);
  });
});
