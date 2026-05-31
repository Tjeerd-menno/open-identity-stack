# Playwright Patterns For AdminWeb

This guide captures the selector and waiting patterns that have held up well in CI for the AdminWeb test suite.

## Condition-Based Waiting

Prefer waiting on a concrete UI or network signal over `WaitForTimeoutAsync`.

- Wait for the exact response tied to the user action, including the search term or route parameter that makes the request unique.
- Wait for visible state that confirms the UI settled, such as a table row appearing, a dialog closing, a toast rendering, or a heading becoming visible.
- Keep fallback `Task.Delay(...)` usage small and local to React settle time after a stronger wait has already completed.

Good examples:

- `WaitForURLAsync("**/Account/Login*")` after auth navigation
- `WaitForTableRowAsync(page, email)` after a create flow
- `WaitForAsync(State = Hidden)` on a loading spinner after detail-page fetches

## Radix And Role-Based Controls

Several AdminWeb controls render as buttons or composite widgets instead of native form inputs.

- Use `GetByRole(...)` with accessible names for checkboxes, dialogs, tabs, and comboboxes.
- For Radix checkbox primitives, use `ClickAsync()` and assert `aria-checked` or `data-state` instead of `CheckAsync()`.
- Scope duplicate action names to the active container, usually `role="dialog"` for modal submit buttons.

Good examples:

- `page.GetByRole(AriaRole.Dialog, new() { Name = "Delete Application", Exact = true })`
- `dialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true })`
- `checkbox.GetAttributeAsync("data-state")`

## Search And Table Flows

List views are prone to false positives when the initial fetch and the search fetch look similar.

- Match the exact search term in the request predicate when waiting for list reloads.
- Search for the row text after the request completes instead of assuming `NetworkIdle` is enough.
- Prefer helper methods that name the user-visible condition you are waiting for.

## Detail And Delete Flows

Destructive flows need two checks: the UI path and the backend state transition.

- Scope confirm buttons to the open dialog.
- Wait for the post-action navigation or toast before asserting on the list page.
- If a delete flow fails because of transaction or projection behavior, keep the test as the signal and fix the backend boundary rather than masking it with broader waits.

## When To Add A Helper

Add or extend a helper in `Helpers/TestHelpers.cs` when:

- The same locator or wait pattern appears in more than one test class.
- The pattern depends on AdminWeb-specific behavior such as Radix widgets or routed list pages.
- The helper can express the user-visible condition better than the raw Playwright calls.
