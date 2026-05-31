# Tasks: Management Web Foundation

**Input**: Design documents from `/specs/007-management-web/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required for the behavior changes in this feature. Test tasks are included before implementation tasks for each user story.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions
- Required tests appear before implementation tasks for the same story

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 [P] Create the ManagementWeb Vite app scaffold in `src/OpenIdentityStack.ManagementWeb/package.json`, `src/OpenIdentityStack.ManagementWeb/tsconfig.json`, `src/OpenIdentityStack.ManagementWeb/vite.config.ts`, `src/OpenIdentityStack.ManagementWeb/index.html`, and `src/OpenIdentityStack.ManagementWeb/src/main.tsx`
- [X] T002 Add the ManagementWeb Aspire resource and local runtime wiring in `src/OpenIdentityStack.AppHost/AppHost.cs`
- [X] T003 [P] Add the ManagementWeb environment sample and launch notes in `src/OpenIdentityStack.ManagementWeb/.env.example` and `src/OpenIdentityStack.ManagementWeb/README.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before any user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create shared admin API client and auth bootstrap helpers in `src/OpenIdentityStack.ManagementWeb/src/lib/admin-api.ts` and `src/OpenIdentityStack.ManagementWeb/src/lib/auth.ts`
- [X] T005 [P] Create the shared shell, theme provider, and navigation primitives in `src/OpenIdentityStack.ManagementWeb/src/components/AppShell.tsx`, `src/OpenIdentityStack.ManagementWeb/src/components/ThemeProvider.tsx`, `src/OpenIdentityStack.ManagementWeb/src/components/Navigation.tsx`, and `src/OpenIdentityStack.ManagementWeb/src/lib/theme-preference.ts`
- [X] T006 [P] Set up the dedicated ManagementWeb end-to-end test project scaffolding in `tests/OpenIdentityStack.ManagementWeb.E2ETests/OpenIdentityStack.ManagementWeb.E2ETests.csproj`, `tests/OpenIdentityStack.ManagementWeb.E2ETests/playwright.config.ts`, and `tests/OpenIdentityStack.ManagementWeb.E2ETests/fixtures/`
- [X] T007 Define the base ManagementWeb routes and placeholder navigation for future domains in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx` and `src/OpenIdentityStack.ManagementWeb/src/routes/placeholder.tsx`

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Operate users in new management UI (Priority: P1) 🎯 MVP

**Goal**: Deliver the first production slice for user lifecycle management in Management Web.

**Independent Test**: An operator can list, inspect, edit, disable, and assign existing roles to users in Management Web without leaving the new UI.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T008 [P] [US1] Add failing vitest coverage for the Users list/detail flow in `src/OpenIdentityStack.ManagementWeb/src/features/users/UsersPage.test.tsx`
- [X] T009 [P] [US1] Add failing Playwright coverage for the Users workflow in `tests/OpenIdentityStack.ManagementWeb.E2ETests/users.spec.ts`

### Implementation for User Story 1

- [X] T010 [P] [US1] Implement the Users list and detail pages in `src/OpenIdentityStack.ManagementWeb/src/features/users/UsersPage.tsx` and `src/OpenIdentityStack.ManagementWeb/src/features/users/UserDetailsPanel.tsx`
- [X] T011 [P] [US1] Implement user edit, enable/disable, and existing-role assignment flows in `src/OpenIdentityStack.ManagementWeb/src/features/users/UserEditForm.tsx` and `src/OpenIdentityStack.ManagementWeb/src/features/users/user-mutations.ts`
- [X] T012 [US1] Wire Users routes and permission-gated actions into the shell in `src/OpenIdentityStack.ManagementWeb/src/routes/users.tsx` and `src/OpenIdentityStack.ManagementWeb/src/components/Navigation.tsx`

**Checkpoint**: User Story 1 should now be independently functional and demoable.

---

## Phase 4: User Story 2 - Use reliable light/dark appearance controls (Priority: P2)

**Goal**: Let operators choose light, dark, or system appearance and persist that choice across sessions.

**Independent Test**: Changing appearance mode persists on reload and re-entry, and system mode is used when no preference exists.

### Tests for User Story 2

- [X] T013 [P] [US2] Add failing vitest coverage for theme preference persistence and fallback behavior in `src/OpenIdentityStack.ManagementWeb/src/lib/theme-preference.test.ts` and `src/OpenIdentityStack.ManagementWeb/src/components/ThemeProvider.test.tsx`

### Implementation for User Story 2

- [X] T014 [P] [US2] Implement light/dark/system preference storage and retrieval in `src/OpenIdentityStack.ManagementWeb/src/lib/theme-preference.ts`
- [X] T015 [US2] Implement the Mantine theme toggle and shell-wide application of the saved preference in `src/OpenIdentityStack.ManagementWeb/src/components/ThemeProvider.tsx` and `src/OpenIdentityStack.ManagementWeb/src/components/ThemeToggle.tsx`

**Checkpoint**: User Stories 1 and 2 should both work independently.

---

## Phase 5: User Story 3 - Run dual UI rollout without operator disruption (Priority: P3)

**Goal**: Keep AdminWeb and Management Web available on separate hostnames with sign-in continuity between them.

**Independent Test**: An operator who is already signed in can move between both UIs without logging in again, and each UI stays independently reachable.

### Tests for User Story 3

- [X] T016 [P] [US3] Add failing Playwright coverage for cross-UI sign-in continuity and separate hostname entry points in `tests/OpenIdentityStack.ManagementWeb.E2ETests/auth-continuity.spec.ts`

### Implementation for User Story 3

- [X] T017 [P] [US3] Configure ManagementWeb hostname, OIDC, and runtime environment values in `src/OpenIdentityStack.AppHost/AppHost.cs` and `src/OpenIdentityStack.ManagementWeb/.env.example`
- [X] T018 [US3] Document parallel rollout, operator access, and rollback expectations in `docs/management-web.md` and `deploy/management-web.md`

**Checkpoint**: All user stories should now be independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T019 [P] Update ManagementWeb operator documentation and screenshots in `src/OpenIdentityStack.ManagementWeb/README.md` and `docs/management-web.md`
- [X] T020 Run the validation commands from `specs/007-management-web/quickstart.md` and fix any issues discovered

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - blocks all user stories
- **User Stories (Phase 3+)**: Depend on Foundational completion
- **Polish (Final Phase)**: Depends on desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational - no dependency on other stories
- **User Story 2 (P2)**: Can start after Foundational - may reuse shell/theme plumbing from US1 but remains independently testable
- **User Story 3 (P3)**: Can start after Foundational - may reuse app/runtime wiring but remains independently testable

### Within Each User Story

- Tests MUST be written before implementation
- Shared primitives before feature pages
- Feature pages before route wiring
- Route wiring before rollout validation
- Story complete before moving to the next priority

### Parallel Opportunities

- Setup tasks T001 and T003 can run in parallel
- Foundational tasks T004, T005, T006, and T007 can run in parallel after Setup
- US1 tests T008 and T009 can run in parallel
- US1 implementation tasks T010 and T011 can run in parallel
- US2 test T013 and implementation tasks T014 can run in parallel with unrelated story work
- US3 test T016 can run in parallel with US2 work once the foundation is complete

---

## Parallel Example: User Story 1

```text
Task: "Add failing vitest coverage for the Users list/detail flow in src/OpenIdentityStack.ManagementWeb/src/features/users/UsersPage.test.tsx"
Task: "Add failing Playwright coverage for the Users workflow in tests/OpenIdentityStack.ManagementWeb.E2ETests/users.spec.ts"
Task: "Implement the Users list and detail pages in src/OpenIdentityStack.ManagementWeb/src/features/users/UsersPage.tsx and src/OpenIdentityStack.ManagementWeb/src/features/users/UserDetailsPanel.tsx"
Task: "Implement user edit, enable/disable, and existing-role assignment flows in src/OpenIdentityStack.ManagementWeb/src/features/users/UserEditForm.tsx and src/OpenIdentityStack.ManagementWeb/src/features/users/user-mutations.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. Stop and validate User Story 1 independently

### Incremental Delivery

1. Complete Setup + Foundational
2. Deliver User Story 1 as the MVP
3. Add User Story 2 without breaking the first slice
4. Add User Story 3 and validate cross-UI rollout behavior
5. Finish with polish, docs, and validation

### Parallel Team Strategy

1. One developer can own ManagementWeb scaffold and AppHost wiring
2. Another can prepare the shell/theme foundation
3. After the foundation, User Story 1, User Story 2, and User Story 3 can proceed in parallel
