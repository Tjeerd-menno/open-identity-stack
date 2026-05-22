---

description: "Task list template for feature implementation"
---

# Tasks: [FEATURE NAME]

**Input**: Design documents from `/specs/[###-feature-name]/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Required for behavior changes per the constitution. Include failing test tasks before implementation unless the spec/plan documents a low-risk docs-only or mechanical-change exception.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions
- Required tests must appear before implementation tasks for the same story
- Security, observability, documentation, and deployment work must be included when the plan identifies impact

## Path Conventions

- **Backend domain/application**: `src/OpenIdentityStack.Domain/`, `src/OpenIdentityStack.Application/`, matching tests under `tests/OpenIdentityStack.*.Tests/`
- **Backend adapters/API**: `src/OpenIdentityStack.Infrastructure/`, `src/OpenIdentityStack.Api/`, `src/OpenIdentityStack.DbMigrator/`, matching tests under `tests/OpenIdentityStack.Infrastructure.Tests/`, `tests/OpenIdentityStack.Api.Tests/`, and `tests/OpenIdentityStack.Contract.Tests/`
- **Aspire/runtime**: `src/OpenIdentityStack.AppHost/`, `src/OpenIdentityStack.ServiceDefaults/`
- **AdminWeb**: `src/OpenIdentityStack.AdminWeb/src/features/[feature]/`, shared UI under `src/OpenIdentityStack.AdminWeb/src/components/`, tests colocated or under E2E project as appropriate
- **Docs/deployment**: `docs/`, `deploy/`

<!--
  ============================================================================
  IMPORTANT: The tasks below are SAMPLE TASKS for illustration purposes only.

  The /speckit-tasks command MUST replace these with actual tasks based on:
  - User stories from spec.md (with their priorities P1, P2, P3...)
  - Feature requirements from plan.md
  - Entities from data-model.md
  - Endpoints from contracts/
  - Security, operational, documentation, dependency, and validation impacts
  - The constitution's package bans and architecture boundaries

  Tasks MUST be organized by user story so each story can be:
  - Implemented independently
  - Tested independently
  - Delivered as an MVP increment

  DO NOT keep these sample tasks in the generated tasks.md file.
  ============================================================================
-->

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create project structure per implementation plan
- [ ] T002 Initialize [language] project with [framework] dependencies
- [ ] T003 [P] Configure linting and formatting tools

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

Examples of foundational tasks (adjust based on your project):

- [ ] T004 Define database schema or migration changes in src/OpenIdentityStack.Infrastructure/Persistence/ or mark N/A in plan
- [ ] T005 [P] Define authentication/authorization and permission boundaries in the affected Api/Application files
- [ ] T006 [P] Configure API routing, validation, Problem Details behavior, and OpenAPI metadata in src/OpenIdentityStack.Api/
- [ ] T007 Create shared domain/application types needed by multiple stories in src/OpenIdentityStack.Domain/ or src/OpenIdentityStack.Application/
- [ ] T008 Configure logging, audit events, health, diagnostics, or safe error behavior in the affected runtime files
- [ ] T009 Setup environment configuration, Aspire wiring, deployment, or rollback support when identified by the plan

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - [Title] (Priority: P1) 🎯 MVP

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T010 [P] [US1] Unit test for [domain/application behavior] in tests/OpenIdentityStack.[Layer].Tests/[Feature]/[Name]Tests.cs
- [ ] T011 [P] [US1] API/integration or contract test for [endpoint/workflow] in tests/OpenIdentityStack.[Api|Contract].Tests/[Feature]/[Name]Tests.cs
- [ ] T012 [P] [US1] AdminWeb Vitest/Playwright test for [workflow] in src/OpenIdentityStack.AdminWeb/src/features/[feature]/ or tests/OpenIdentityStack.AdminWeb.E2ETests/ if UI is affected

### Implementation for User Story 1

- [ ] T013 [P] [US1] Create or update domain model/value object in src/OpenIdentityStack.Domain/[Feature]/[Name].cs
- [ ] T014 [P] [US1] Create or update application command/query/use case in src/OpenIdentityStack.Application/[Feature]/[Name].cs
- [ ] T015 [US1] Implement infrastructure adapter or persistence change in src/OpenIdentityStack.Infrastructure/[Feature]/[Name].cs
- [ ] T016 [US1] Implement API endpoint/request/response mapping in src/OpenIdentityStack.Api/[Feature]/[Name].cs
- [ ] T017 [US1] Add validation, authorization, safe error handling, and audit/logging for user story 1 operations

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - [Title] (Priority: P2)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 2

- [ ] T018 [P] [US2] Unit test for [domain/application behavior] in tests/OpenIdentityStack.[Layer].Tests/[Feature]/[Name]Tests.cs
- [ ] T019 [P] [US2] API/integration, contract, or AdminWeb test for [workflow] in the exact affected test project

### Implementation for User Story 2

- [ ] T020 [P] [US2] Create or update [domain/application/API/AdminWeb artifact] in [exact path]
- [ ] T021 [US2] Implement [use case/service/hook/component] in [exact path]
- [ ] T022 [US2] Implement [endpoint/feature/workflow] in [exact path]
- [ ] T023 [US2] Integrate with User Story 1 components (if needed)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - [Title] (Priority: P3)

**Goal**: [Brief description of what this story delivers]

**Independent Test**: [How to verify this story works on its own]

### Tests for User Story 3

- [ ] T024 [P] [US3] Unit test for [domain/application behavior] in tests/OpenIdentityStack.[Layer].Tests/[Feature]/[Name]Tests.cs
- [ ] T025 [P] [US3] API/integration, contract, or AdminWeb test for [workflow] in the exact affected test project

### Implementation for User Story 3

- [ ] T026 [P] [US3] Create or update [domain/application/API/AdminWeb artifact] in [exact path]
- [ ] T027 [US3] Implement [use case/service/hook/component] in [exact path]
- [ ] T028 [US3] Implement [endpoint/feature/workflow] in [exact path]

**Checkpoint**: All user stories should now be independently functional

---

[Add more user story phases as needed, following the same pattern]

---

## Phase N: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] TXXX [P] Documentation updates in docs/
- [ ] TXXX Code cleanup and refactoring
- [ ] TXXX Performance optimization across all stories
- [ ] TXXX [P] Additional unit/API/contract/AdminWeb tests in the affected test projects
- [ ] TXXX Security hardening
- [ ] TXXX Observability, audit, health, or diagnostics updates
- [ ] TXXX Deployment, migration, rollback, or Aspire/AppHost updates
- [ ] TXXX Run quickstart.md validation
- [ ] TXXX Run final validation commands from plan.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3)
- **Polish (Final Phase)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - May integrate with US1 but should be independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - May integrate with US1/US2 but should be independently testable

### Within Each User Story

- Required tests MUST be written and FAIL before implementation
- Models before services
- Services before endpoints
- Core implementation before integration
- Security, audit, validation, and safe error behavior before story checkpoint
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel
- Models within a story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Unit test for [behavior] in tests/OpenIdentityStack.Application.Tests/[Feature]/[Name]Tests.cs"
Task: "API or contract test for [endpoint] in tests/OpenIdentityStack.Api.Tests/[Feature]/[Name]Tests.cs"

# Launch all models for User Story 1 together:
Task: "Create [Entity1] in src/OpenIdentityStack.Domain/[Feature]/[Entity1].cs"
Task: "Create [Entity2] in src/OpenIdentityStack.Domain/[Feature]/[Entity2].cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test independently → Deploy/Demo
4. Add User Story 3 → Test independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1
   - Developer B: User Story 2
   - Developer C: User Story 3
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Use exact repository paths in every task
- Do not use banned packages: Newtonsoft.Json, Swashbuckle/Swagger, AutoMapper, MediatR
- Preserve Clean/Hexagonal dependency direction and AdminWeb feature-folder patterns
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
