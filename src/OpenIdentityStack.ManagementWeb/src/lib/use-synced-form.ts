import { useEffect } from 'react';
import type { UseFormReturnType } from '@mantine/form';

/**
 * Keeps a Mantine form in step with server state.
 *
 * Detail pages seed a form from a fetched entity, then have to reseed it whenever that entity
 * changes — after a refetch, or when the route switches to a different record. Writing the
 * effect inline forces every call site to suppress `react-hooks/exhaustive-deps`, because a
 * Mantine form object is a new identity on every render and listing it as a dependency loops
 * forever. Centralising it here means one suppression instead of one per page, and — more to
 * the point — one shared answer to *when* a form reseeds.
 *
 * `values` is compared by content, so a refetch that returns identical values leaves the
 * user's in-progress edits alone, while a genuine server-side change reseeds the form and
 * resets its dirty state.
 */
export function useSyncedForm<T extends Record<string, unknown>>(
  form: UseFormReturnType<T>,
  values: T
): void {
  // Content comparison: the object identity differs on every render, the contents rarely do.
  const serialized = JSON.stringify(values);

  useEffect(() => {
    const next = JSON.parse(serialized) as T;
    form.setValues(next);
    form.resetDirty(next);
    // `form` is intentionally omitted: Mantine returns a fresh object each render, so including
    // it would re-run this effect on every render and clobber whatever the user is typing.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [serialized]);
}
