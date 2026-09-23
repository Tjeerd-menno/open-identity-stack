import { useDebouncedValue } from '@mantine/hooks';
import { useEffect, useRef, useState } from 'react';

export const SEARCH_DEBOUNCE_MS = 300;

/**
 * Keeps a locally-typed draft that is only pushed to the parent (and therefore
 * the query key) after the user pauses, so list pages issue one request per
 * search term instead of one per keystroke. External resets of `value`
 * (e.g. "Clear all") are still reflected immediately.
 */
export function useDebouncedSearch(value: string, onChange: (value: string) => void, delay = SEARCH_DEBOUNCE_MS) {
  const [draft, setDraft] = useState(value);
  const [debounced] = useDebouncedValue(draft, delay);
  const emitted = useRef(value);
  const onChangeRef = useRef(onChange);

  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  useEffect(() => {
    if (debounced !== emitted.current) {
      emitted.current = debounced;
      onChangeRef.current(debounced);
    }
  }, [debounced]);

  useEffect(() => {
    if (value !== emitted.current) {
      emitted.current = value;
      setDraft(value);
    }
  }, [value]);

  return [draft, setDraft] as const;
}
