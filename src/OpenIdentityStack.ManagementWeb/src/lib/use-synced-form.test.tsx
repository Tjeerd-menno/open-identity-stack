import { describe, expect, it } from 'vitest';
import { act, render } from '@testing-library/react';
import { useForm } from '@mantine/form';
import { useSyncedForm } from './use-synced-form';

type Values = { displayName: string };

function Harness({ displayName }: { displayName: string }) {
  const form = useForm<Values>({ initialValues: { displayName } });
  useSyncedForm(form, { displayName });
  return (
    <div>
      <span data-testid="value">{form.values.displayName}</span>
      <span data-testid="dirty">{String(form.isDirty())}</span>
      <button type="button" onClick={() => form.setFieldValue('displayName', 'edited locally')}>
        edit
      </button>
    </div>
  );
}

describe('useSyncedForm', () => {
  it('seeds the form from the supplied values', () => {
    const view = render(<Harness displayName="Original" />);
    expect(view.getByTestId('value').textContent).toBe('Original');
  });

  it('reseeds the form when the server value changes', () => {
    const view = render(<Harness displayName="Original" />);

    view.rerender(<Harness displayName="Renamed on the server" />);

    expect(view.getByTestId('value').textContent).toBe('Renamed on the server');
    expect(view.getByTestId('dirty').textContent).toBe('false');
  });

  it('leaves in-progress edits alone when a refetch returns identical values', () => {
    // This is the regression the shared hook exists to prevent: a re-render with unchanged
    // server data must not wipe what the user is typing.
    const view = render(<Harness displayName="Original" />);
    act(() => {
      view.getByRole('button', { name: 'edit' }).click();
    });
    expect(view.getByTestId('value').textContent).toBe('edited locally');

    view.rerender(<Harness displayName="Original" />);

    expect(view.getByTestId('value').textContent).toBe('edited locally');
  });
});
