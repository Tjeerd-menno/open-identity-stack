# Research: Management Web Foundation

## 1. Separate frontend app

- **Decision**: Create Management Web as a peer application beside AdminWeb rather than folding it into the existing app.
- **Rationale**: The grilling decisions explicitly favor parallel rollout, independent deployability, and a stronger UI quality bar without blocking current AdminWeb work.
- **Alternatives considered**: Extend AdminWeb in place; share one deployment artifact; gate the new UI behind a path split.

## 2. Mantine as the UI foundation

- **Decision**: Use Mantine as the primary component and theming system for the new frontend.
- **Rationale**: The feature request explicitly calls for a more professional, production-grade management UI with light/dark mode support.
- **Alternatives considered**: Keep the existing UI stack; mix multiple component systems; custom-build the shell and controls.

## 3. Theme preference handling

- **Decision**: Support light, dark, and system appearance with system fallback on first load and persisted preference afterward.
- **Rationale**: This gives operators predictable appearance behavior while honoring the explicit requirement for theme control.
- **Alternatives considered**: Hardcoded dark mode; per-session-only preference; no system mode.

## 4. Parallel rollout topology

- **Decision**: Keep AdminWeb and Management Web on separate hostnames with cross-UI sign-in continuity.
- **Rationale**: This matches the grilled rollout posture and keeps each UI independently operable and deployable.
- **Alternatives considered**: Single-host path routing; immediate replacement; staged canary rollout.

## 5. Backend integration model

- **Decision**: Reuse the existing admin API as the source of truth for permissions and user-management workflows.
- **Rationale**: The context decisions prefer shared backend policy rather than UI-specific rule duplication.
- **Alternatives considered**: Introduce a new frontend-specific backend; mirror data into a BFF; duplicate authorization rules in the UI.

## 6. Scope of phase 1

- **Decision**: Focus phase 1 on the Users vertical slice, navigation scaffolding for later domains, and the production-grade shell experience.
- **Rationale**: This keeps the initial release small enough to ship while still proving the new UI direction.
- **Alternatives considered**: Big-bang parity across all domains; shell-only launch; bulk operations in the first phase.
