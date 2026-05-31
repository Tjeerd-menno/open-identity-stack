## Summary

- Describe the user-visible or operational change.

## Validation

- List the commands or checks you ran.

## Review Checklist

- [ ] OpenAPI or contract changes are intentional, and any `info.version` change is called out.
- [ ] Renames update routes, schemas, validation messages, tests, and UI labels consistently.
- [ ] E2E coverage uses condition-based waits instead of fixed sleeps.
- [ ] Radix or role-based controls are tested with role/state-aware selectors.
- [ ] Transactional delete or manifest flows were reviewed for projection and persistence ordering.
- [ ] The PR scope is intentionally small enough to review end to end.

## Notes

- Link the relevant issue, spec, or follow-up work.
