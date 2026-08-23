import { describe, expect, it } from 'vitest';
import { act, render } from '@testing-library/react';
import { useForm } from '@mantine/form';
import { useSyncedForm } from './use-synced-form';

type Values = { displayName: string };

function Harness({ recordId, displayName }: { recordId: string; displayName: string }) {
  const form = useForm<Values>({ initialValues: { displayName } });
  useSyncedForm(form, { displayName }, recordId);
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

function edit(view: ReturnType<typeof render>) {
  act(() => {
    view.getByRole('button', { name: 'edit' }).click();
  });
}

describe('useSyncedForm', () => {
  it('seeds the form from the supplied values', () => {
    const view = render(<Harness recordId="a" displayName="Original" />);
    expect(view.getByTestId('value').textContent).toBe('Original');
  });

  it('reseeds the form when the server value changes', () => {
    const view = render(<Harness recordId="a" displayName="Original" />);

    view.rerender(<Harness recordId="a" displayName="Renamed on the server" />);

    expect(view.getByTestId('value').textContent).toBe('Renamed on the server');
    expect(view.getByTestId('dirty').textContent).toBe('false');
  });

  it('leaves in-progress edits alone when a refetch returns identical values', () => {
    // A re-render with unchanged server data must not wipe what the user is typing.
    const view = render(<Harness recordId="a" displayName="Original" />);
    edit(view);
    expect(view.getByTestId('value').textContent).toBe('edited locally');

    view.rerender(<Harness recordId="a" displayName="Original" />);

    expect(view.getByTestId('value').textContent).toBe('edited locally');
  });

  it('reseeds when switching records even if their values are identical', () => {
    // The reason recordId is part of the key: without it an unsaved edit on record "a" would
    // follow the user onto record "b", which happens to carry the same display name.
    const view = render(<Harness recordId="a" displayName="Shared name" />);
    edit(view);
    expect(view.getByTestId('value').textContent).toBe('edited locally');

    view.rerender(<Harness recordId="b" displayName="Shared name" />);

    expect(view.getByTestId('value').textContent).toBe('Shared name');
    expect(view.getByTestId('dirty').textContent).toBe('false');
  });
});
