# Dependable credential termination implementation plan

Baseline: `7abf59bc7951433ea821497ab919bd8aa4e4fbd5`. Scope: assessment recommendation 2 (F06, F10–F12, F17).

## Contract

- Logout/revoke-one terminates that user session and all credentials derived from it, including offline refresh credentials. Other sessions remain valid. Revoke-all, password reset, and disable terminate every affected user session. Re-enabling a user must not revive an old session.
- Persisted session validity is authoritative for OP cookies, user authorization/code/refresh, local bearer authentication, UserInfo, and introspection. Missing, malformed, expired, terminated, foreign-owned, or obsolete sessions fail closed. Client-credentials grants are explicitly distinct from user sessions.
- After a termination commit, the next local validation must reject retained credentials. An already-authorized in-flight request may finish. Concurrent issuance must not produce a credential that passes subsequent validation after termination.
- Refresh recomputes current user/role/permission/group facts, preserving granted scope/resource and authentication-time constraints. Privilege reduction must also be enforced for existing access at local boundaries.
- Remote APIs must use authoritative introspection for next-request enforcement; independently validated self-contained tokens are bounded by expiry. RP cookie eviction depends on receiver support. Durable retries preserve notification work through cancellation/restart; an unreachable RP cannot be promised a fixed eviction deadline. Observe delivery lag and failure.
- Local termination and actor/target/reason audit must commit consistently. Notification failure cannot restore local credentials. Delivery is idempotent and resumable, with no credentials in audit/log payloads.
- Hintless browser logout requires explicit anti-forgery-protected confirmation. Front-channel frames must actually render with the configured issuer. The session-check iframe needs a narrowly scoped embedding policy; other pages retain protection.

## Work and ownership

- [x] A1 — Add failing domain/application/infrastructure tests for ownership/status/version validation, reset/disable eviction, atomic audited revoke-one/all, and failure/concurrency cases.
- [x] A2 — Implement authoritative session validation and persisted invalidation/version state; wire reset/disable/revoke operations; add migration and persistence verification. Owner: Terra lifecycle agent. Own Domain, Application user/reset/disable and session validation/revoke/create, persistence, lifecycle Infrastructure services. Do not edit API or logout protocol files.
- [x] B1 — Add failing authentication tests for session-creation failure, missing/malformed/deleted/foreign sessions, old cookies/access/refresh/code, disabled users, and stale authorization.
- [x] B2 — Implement fail-closed authentication and current authorization projection, cookie validation, bearer and introspection enforcement. Owner: Terra authentication agent. Own AccountController, AuthorizationController, TokenClaimProjectionService, new authentication handlers, related tests. Do not edit LogoutController or shared persistence. Register new services through a dedicated extension; coordinator merges Program/DI edits.
- [x] C1 — Add failing logout tests for normal client registration metadata, confirmation/CSRF, rendered frames, iframe headers, cancellation/retry/restart, and actor audit integration.
- [x] C2 — Implement logout protocol, client metadata capture, durable notification retry using persisted client delivery state, correct issuer/headers, and scoped logout confirmation. Owner: Terra logout agent. Own LogoutController/views, ProcessLogout/AddClientSession, notification infrastructure/worker, ServiceDefaults headers, related tests. Coordinate repository additions with lifecycle agent; do not modify migrations independently.
- [x] D — Coordinator integrates shared DI/contracts, runs build and appropriate backend/API/contract suites, fills sequence-test gaps, and verifies migration/docs. Container runtime: Podman. Test execution sharing output folders is serialized.
- [x] E — Main-session review against this contract and repository standards; fix findings and rerun affected verification. Record results and rollout constraints. No commit or PR unless requested.

## Fixed integration seams

Lifecycle agent supplies Application abstraction `ICredentialSessionValidator` with `Task<bool> IsValidAsync(UserId userId, SessionId sessionId, CancellationToken cancellationToken = default)`.

Lifecycle agent supplies Application abstraction `ICredentialTerminationService` with `Task<Result> TerminateSessionAsync(SessionId sessionId, string actorId, string reason, bool isLogout = false, CancellationToken cancellationToken = default)`. It persists terminal state and actor audit atomically; repeat termination succeeds without reactivation. Logout processing delegates local termination here, then notification can resume independently. Existing command response shapes should remain compatible where possible.

Persist pending RP notification state atomically with terminal session state using existing client-session delivery fields where sufficient; add missing fields via the lifecycle agent's migration. Workers retry persisted pending/failed delivery, use bounded backoff, and never require the original browser request to remain alive.

## Acceptance evidence and rollout

Keep explicit red/green evidence. Replace misleading refresh tests with real token endpoint sequences. Cover unaffected sessions and machine credentials as positive controls. Include delayed/repeated notification and invalidation across separate service scopes. Verify actual authorization failures, not only session-row status.

Deploy schema before dependent code. Old credentials without required session linkage are rejected and users must reauthenticate. Avoid rolling mixed versions that still accept invalidated credentials. Backfill/version behavior and rollback must preserve revocation; restoring earlier permissive authentication code is not a security-safe rollback. Document external introspection/cache requirements and RP delivery monitoring.

Implementation and main-session review are complete. See [implementation, verification, and rollout](credential-termination-implementation.md).
