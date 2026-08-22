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
 * The form reseeds when either `recordId` or the contents of `values` change:
 *
 * - A refetch returning identical values for the same record leaves in-progress edits alone.
 * - A server-side change to the record reseeds the form and resets its dirty state.
 * - Switching to a different record always reseeds, even when its values happen to be
 *   identical to the previous record's — otherwise an unsaved edit would follow the user
 *   onto a record it was never made against.
 *
 * @param form The Mantine form to keep in sync.
 * @param values The server-provided values the form should hold.
 * @param recordId Identifies the record `values` came from. Required: without it, navigating
 *   between two records with equal values would not reseed.
 */
export function useSyncedForm<T extends Record<string, unknown>>(
  form: UseFormReturnType<T>,
  values: T,
  recordId: string
): void {
  // Content comparison: the object identity differs on every render, the contents rarely do.
  const syncKey = JSON.stringify([recordId, values]);

  useEffect(() => {
    const [, next] = JSON.parse(syncKey) as [string, T];
    form.setValues(next);
    form.resetDirty(next);
    // `form` is intentionally omitted: Mantine returns a fresh object each render, so including
    // it would re-run this effect on every render and clobber whatever the user is typing.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [syncKey]);
}
