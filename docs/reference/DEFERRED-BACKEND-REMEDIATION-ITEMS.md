# Deferred backend remediation items

This document tracks high-impact backend improvements that were intentionally deferred during the maintainability pass because they require explicit product/security sign-off due to behavior impact.

## Context

The backend review and remediation pass completed a set of behavior-preserving improvements (CQS cleanup, DI/composition cleanup, startup extraction, error-handling deduplication, and query N+1 reduction). The remaining items below were not blocked by test infrastructure; they were deferred because they alter runtime behavior in sensitive areas.

Current verification baseline (green at defer time):

- Full solution build (0 warnings, 0 errors)
- Domain.Tests
- Application.Tests
- Infrastructure.Tests
- Api.UnitTests
- Api.Tests
- Contract.Tests

## Deferred item 1: transactional write boundaries across domain/projection/audit

**Status:** Deferred (requires architecture decision)  
**Area:** Application + Infrastructure  
**Risk class:** High (data consistency semantics)

### Problem

Some command flows can involve multiple persistence side effects (domain entity mutation, projection updates, audit logging). A failure in later steps may leave partially applied outcomes if a unified transaction boundary is not guaranteed for the complete write operation.

### Why deferred

Introducing a true cross-repository unit-of-work/transaction model is a broad architectural change:

- touches many command use cases and repository boundaries
- changes partial-failure behavior and observable rollback semantics
- requires clear policy for audit durability vs. business write durability

This exceeds a safe refactor-only scope.

### Recommended implementation direction

1. Introduce a single application-level transaction abstraction owned by Infrastructure (for EF-backed write flows).
2. Define one authoritative policy for audit writes:
   - same transaction as business write, or
   - outbox/evented pattern with explicit eventual consistency guarantees.
3. Migrate write use cases incrementally behind integration tests for failure/rollback paths.
4. Add explicit telemetry for transaction rollback and partial-write detection.

### Acceptance criteria

- Multi-step command operations are atomic by design (or explicitly eventual with documented semantics).
- Simulated failures in post-domain steps do not leave undetected inconsistent state.
- Audit behavior is deterministic and documented per policy.

### Validation requirements

- Integration tests that force failures between write stages and assert final DB state.
- Contract tests (where relevant) confirming unchanged API error contracts or intentionally updated contracts.

## Deferred item 2: authentication settings provider identifier primitive obsession

**Status:** Deferred (requires contract decision)  
**Area:** Application + API + persistence mappings  
**Risk class:** Medium (API/persistence contract churn)

### Problem

`DefaultProviderId` currently flows as a plain string through commands/queries and persistence. This weakens invariants and allows invalid identifiers to survive longer than necessary.

### Why deferred

Changing to a strongly typed value object would cross public API and storage boundaries:

- DTO/request/response contract changes may be needed
- serialization and EF mapping changes are required
- migration/backward-compatibility path must be defined

Without an approved wire-contract strategy, this is not a safe maintainability-only edit.

### Recommended implementation direction

1. Introduce an internal value object (e.g., `ProviderReference`) with validation rules.
2. Add translators at API boundaries first to avoid immediate wire-breaks.
3. Migrate persistence mapping to explicit converter once contract compatibility is decided.
4. Deprecate raw string paths after transition.

### Acceptance criteria

- Provider identifiers are validated at construction, not scattered across handlers.
- No invalid provider references persist beyond boundary translation.
- Backward compatibility (or explicit versioning) is documented.

### Validation requirements

- Unit tests for value object invariants.
- API tests for backward compatibility and error payload consistency.
- Migration tests for existing persisted settings.

## Deferred item 3: logout flow completion (`id_token_hint` and session resolution)

**Status:** Deferred (feature-completion decision)  
**Area:** API authentication/logout  
**Risk class:** High (security/session behavior)

### Problem

Current logout flow has TODO paths for:

- extracting initiating client from `id_token_hint`
- resolving current session from additional sources beyond claims

Core cookie sign-out is in place, but fully resolving initiator/session for complete SLO behavior is not yet implemented.

### Why deferred

Implementing these TODOs is feature completion, not pure refactoring:

- can change RP-initiated logout behavior and affected-session scope
- requires strict security validation for token parsing/validation and claim trust boundaries
- must be coordinated with front-channel/back-channel logout expectations

For an IAM product, these changes require explicit security owner approval.

### Recommended implementation direction

1. Decode/validate `id_token_hint` via OpenIddict primitives, not ad-hoc parsing.
2. Define trusted session resolution order (claims, auth context, secure session cookie) with explicit precedence.
3. Add threat-model checks for forged/mismatched hints and stale sessions.
4. Add compatibility tests for RP-initiated logout scenarios across clients.

### Acceptance criteria

- Initiating client and target session are resolved deterministically and securely.
- Logout behavior is spec-aligned for supported OIDC flows.
- No regression in cookie/session termination guarantees.

### Validation requirements

- API integration tests for logout variants (`id_token_hint` present/absent/invalid).
- Security-focused negative tests for tampered token hints.
- End-to-end session termination verification across SLO channels.

### Resolved separately: back-channel logout token signing

The logout token itself was a distinct defect and is **fixed**, not deferred.
`BackChannelLogoutNotifier` previously hand-assembled an `alg: none` JWT — unsigned, and
carrying the session id in `sub`. It now delegates to `LogoutTokenFactory`, which signs with the
OpenIddict server's asymmetric key, sets `typ: logout+jwt`, emits `sid` (not a fabricated `sub`),
and fails closed if no asymmetric signing key or issuer can be resolved rather than degrading to
an unsigned token. Covered by `LogoutTokenFactoryTests`.

## Prioritization proposal

1. **Logout flow completion** (security + standards impact)
2. **Transactional write boundary strategy** (data integrity impact)
3. **Provider identifier typing** (design-quality impact, lower immediate risk)

## Decision gates before implementation

Before any of the above moves from deferred to active, confirm:

- Product owner approval for behavior changes
- Security owner approval for logout and transaction semantics
- Contract/versioning strategy for any public shape changes
- Dedicated test plan and rollout plan

