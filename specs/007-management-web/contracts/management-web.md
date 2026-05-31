# Management Web Contract

## Purpose

Defines the expected operator-facing behavior for the new Management Web app.

## Contract surface

- **Entry point**: Separate hostname from AdminWeb.
- **Authentication**: Must reuse the active identity-provider session when present.
- **Theme behavior**: Must support light, dark, and system appearance.
- **Primary workflows**: Users management is the first functional slice.
- **Navigation**: Future domains may appear as placeholders, but Users must be fully usable in phase 1.
- **Backend dependency**: Management Web consumes the existing admin API and does not own authorization rules.

## Acceptance expectations

1. An authenticated operator can open Management Web without a second login when already signed in elsewhere.
2. The operator can switch appearance mode and see the choice persist on return.
3. The operator can complete the Users workflow without leaving the new UI.
4. When backend authorization denies an action, the UI surfaces a clear permission message.
