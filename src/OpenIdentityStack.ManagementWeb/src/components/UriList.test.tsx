import { fireEvent, render } from '@testing-library/react';
import { MantineProvider } from '@mantine/core';
import { useState } from 'react';
import { describe, expect, it } from 'vitest';
import { UriList } from './UriList';

function Harness({ initialValues = ['first', 'second', 'third'] }: { initialValues?: string[] }) {
  const [values, setValues] = useState(initialValues);

  return (
    <UriList
      label="Redirect URIs"
      description="Where the authorization server returns the user after sign-in."
      values={values}
      onChange={setValues}
      placeholder="https://app.example.com/callback"
    />
  );
}

function ControlledHarness({ values }: { values: string[] }) {
  return (
    <UriList
      label="Redirect URIs"
      description="Where the authorization server returns the user after sign-in."
      values={values}
      onChange={() => {}}
      placeholder="https://app.example.com/callback"
    />
  );
}

describe('UriList', () => {
  it('keeps the remaining URI input attached to its value when an earlier row is removed', () => {
    const view = render(
      <MantineProvider>
        <Harness />
      </MantineProvider>
    );
    const inputs = view.getAllByPlaceholderText('https://app.example.com/callback');
    const thirdInput = inputs[2];

    fireEvent.click(view.getAllByRole('button', { name: 'Remove URI' })[0]);

    const remainingInputs = view.getAllByPlaceholderText('https://app.example.com/callback');
    expect(remainingInputs.map((input) => (input as HTMLInputElement).value)).toEqual(['second', 'third']);
    expect(remainingInputs[1]).toBe(thirdInput);
  });

  it('keeps duplicate URI rows distinct when one is removed', () => {
    const view = render(
      <MantineProvider>
        <Harness initialValues={['same', 'same', 'third']} />
      </MantineProvider>
    );
    const inputs = view.getAllByPlaceholderText('https://app.example.com/callback');

    fireEvent.click(view.getAllByRole('button', { name: 'Remove URI' })[0]);

    const remainingInputs = view.getAllByPlaceholderText('https://app.example.com/callback');
    expect(remainingInputs.map((input) => (input as HTMLInputElement).value)).toEqual(['same', 'third']);
    expect(remainingInputs[0]).toBe(inputs[1]);
    expect(remainingInputs[1]).toBe(inputs[2]);
  });

  it('allows adding the first URI when the controlled list starts empty', () => {
    const view = render(
      <MantineProvider>
        <Harness initialValues={[]} />
      </MantineProvider>
    );
    expect(view.queryByPlaceholderText('https://app.example.com/callback')).not.toBeInTheDocument();

    fireEvent.click(view.getByRole('button', { name: 'Add URI' }));

    const input = view.getByPlaceholderText('https://app.example.com/callback') as HTMLInputElement;
    expect(input.value).toBe('');
  });

  it('renders rows when the controlled values array grows outside the component', () => {
    const view = render(
      <MantineProvider>
        <ControlledHarness values={['first', 'second']} />
      </MantineProvider>
    );

    view.rerender(
      <MantineProvider>
        <ControlledHarness values={['added by parent', 'first', 'second']} />
      </MantineProvider>
    );

    const inputs = view.getAllByPlaceholderText('https://app.example.com/callback');
    expect(inputs.map((input) => (input as HTMLInputElement).value)).toEqual(['added by parent', 'first', 'second']);

    view.rerender(
      <MantineProvider>
        <ControlledHarness values={['second']} />
      </MantineProvider>
    );

    const remainingInputs = view.getAllByPlaceholderText('https://app.example.com/callback');
    expect(remainingInputs).toHaveLength(1);
    expect((remainingInputs[0] as HTMLInputElement).value).toBe('second');
  });

  it('keeps the controlled rows when the parent rejects an add request', () => {
    const view = render(
      <MantineProvider>
        <ControlledHarness values={['first', 'second']} />
      </MantineProvider>
    );

    fireEvent.click(view.getByRole('button', { name: 'Add URI' }));

    const inputs = view.getAllByPlaceholderText('https://app.example.com/callback');
    expect(inputs.map((input) => (input as HTMLInputElement).value)).toEqual(['first', 'second']);
  });

  it('keeps the controlled rows when the parent rejects a remove request', () => {
    const view = render(
      <MantineProvider>
        <ControlledHarness values={['first', 'second']} />
      </MantineProvider>
    );

    fireEvent.click(view.getAllByRole('button', { name: 'Remove URI' })[0]);

    const inputs = view.getAllByPlaceholderText('https://app.example.com/callback');
    expect(inputs.map((input) => (input as HTMLInputElement).value)).toEqual(['first', 'second']);
  });
});
