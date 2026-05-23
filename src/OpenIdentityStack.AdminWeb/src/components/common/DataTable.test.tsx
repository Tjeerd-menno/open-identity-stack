import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { DataTable } from './DataTable';

interface RowData {
  id: string;
  name: string;
  active: boolean;
}

const columns = [
  { header: 'Name', accessorKey: 'name' as const },
  {
    header: 'Status',
    id: 'status',
    cell: ({ row }: { row: RowData }) => (row.active ? 'Active' : 'Disabled'),
  },
];

const data: RowData[] = [
  { id: '1', name: 'Alice', active: true },
  { id: '2', name: 'Bob', active: false },
];

describe('DataTable', () => {
  it('shows a loading spinner when loading', () => {
    const { container } = render(<DataTable<RowData> columns={columns} data={[]} isLoading />);

    expect(container.querySelector('.animate-spin')).toBeInTheDocument();
  });

  it('shows the empty message when there is no data', () => {
    render(<DataTable<RowData> columns={columns} data={[]} emptyMessage="Nothing here" />);

    expect(screen.getByText('Nothing here')).toBeInTheDocument();
  });

  it('renders rows using accessor keys and custom cell renderers', () => {
    render(<DataTable<RowData> columns={columns} data={data} />);

    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('Disabled')).toBeInTheDocument();
  });

  it('renders a search input and reports changes', () => {
    const onChange = vi.fn();

    render(
      <DataTable<RowData>
        columns={columns}
        data={data}
        search={{ value: '', onChange, placeholder: 'Search users' }}
      />
    );

    fireEvent.change(screen.getByPlaceholderText('Search users'), { target: { value: 'adm' } });

    expect(onChange).toHaveBeenCalledWith('adm');
  });

  it('shows pagination details and calls page change handlers', async () => {
    const user = userEvent.setup();
    const onPageChange = vi.fn();

    render(
      <DataTable<RowData>
        columns={columns}
        data={data}
        pagination={{
          page: 2,
          pageSize: 10,
          totalPages: 3,
          totalCount: 25,
          onPageChange,
        }}
      />
    );

    expect(screen.getByText('Showing 11 to 20 of 25 results')).toBeInTheDocument();
    expect(screen.getByText('Page 2 of 3')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /previous/i }));
    await user.click(screen.getByRole('button', { name: /next/i }));

    expect(onPageChange).toHaveBeenNthCalledWith(1, 1);
    expect(onPageChange).toHaveBeenNthCalledWith(2, 3);
  });

  it('disables pagination buttons on the first and last pages', () => {
    const onPageChange = vi.fn();
    const { rerender } = render(
      <DataTable<RowData>
        columns={columns}
        data={data}
        pagination={{
          page: 1,
          pageSize: 10,
          totalPages: 3,
          totalCount: 25,
          onPageChange,
        }}
      />
    );

    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /next/i })).not.toBeDisabled();

    rerender(
      <DataTable<RowData>
        columns={columns}
        data={data}
        pagination={{
          page: 3,
          pageSize: 10,
          totalPages: 3,
          totalCount: 25,
          onPageChange,
        }}
      />
    );

    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /previous/i })).not.toBeDisabled();
  });
});
