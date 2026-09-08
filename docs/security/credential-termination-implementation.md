# Dependable credential termination

This implements recommendation 2 of the OIDC/IAM assessment. The [plan](credential-termination-plan.md) defines the contract. The [research](credential-termination-current-state.md) describes the pre-implementation baseline at `7abf59bc7951433ea821497ab919bd8aa4e4fbd5`.

## Enforcement

The persisted user session is authoritative for interactive cookies, authorization codes, refresh tokens, local bearer authentication, UserInfo, and introspection. User credentials must identify an active, unexpired session owned by their subject. The session's captured security version must match the active user. Missing or invalid linkage fails closed. A server-issued token-kind claim distinguishes client-credentials tokens from user credentials; user-controlled group claims cannot supply that bypass.

Revoke-one and logout terminate one session. Revoke-all, password reset, and disabling a user invalidate all affected sessions. Revoke-all can retain an explicitly excluded session at the new security version. Enabling a user does not restore terminated sessions. Terminal state, pending RP delivery state, and the operation's audit entry commit transactionally. Concurrency tokens prevent a stale session update from restoring an active status.

The next local validation after the termination commit rejects retained credentials. Requests already authorized may finish. Local bearer validation recalculates roles and permissions, and code/refresh exchange rebuilds current subject and group claims. Privilege removal therefore applies to retained local access tokens as well as refreshed tokens.

## Relying-party logout

Register RP endpoints as string values in the OpenIddict application's `Properties` object:

```json
{
  "frontchannel_logout_uri": "https://rp.example/logout/frontchannel",
  "backchannel_logout_uri": "https://rp.example/logout/backchannel"
}
```

Only trusted client provisioning should write these properties. The resolver accepts absolute HTTP(S) URIs and normal authorization captures them in the participating client session. This change does not add management-UI fields for these properties. Existing client sessions need another authorization visit to capture newly configured endpoints.

Browser OIDC logout displays an antiforgery-protected confirmation before changing state. Front-channel logout renders actual frames with `sid` and the configured issuer. Only the logout page receives the required RP frame sources; the session-check iframe receives its embedding exception, while other pages retain their frame protection.

Back-channel notification work is durable. A hosted worker polls every minute, processes up to 100 due sessions, and backs off failed deliveries from two minutes to a maximum interval of one hour. Pending work survives request cancellation and process restart. Completed work is excluded from retries. A crash between an RP accepting a notification and its completion being saved can cause a duplicate; receivers must support idempotent logout. Delivery is at least once, with no fixed deadline for an unreachable RP.

Monitor worker errors and the persisted client-session delivery fields (`LogoutStatus`, `LogoutAttemptCount`, `NextLogoutAttemptAt`, `LogoutCompletedAt`) for outstanding work and delivery lag. Front-channel delivery depends on browser behavior and RP support; rendering an iframe is not proof that the RP cookie was removed.

## Deployment and rollback

Apply migration `20260908093221_AddCredentialLifecycle` before deploying the dependent application version. Both user and session security versions start at zero for existing rows; subsequent invalidation advances the user version. Newly issued access tokens include the standard session ID. Existing user access tokens without that linkage are rejected and require reauthentication.

Configure `OpenIddict:Issuer` explicitly, using the same canonical issuer that relying parties trust. Background notification must not depend on an ambient HTTP request to resolve it.

Avoid mixed deployments containing older instances that accept invalidated credentials. Independent remote JWT validation remains bounded by token expiry; remote APIs needing next-request revocation must use authoritative introspection with no stale positive cache. RP sessions have their own eviction requirements.

Keep the added schema and terminal state during rollback. Rolling back to the earlier permissive authentication implementation can restore access with old credentials and is not a security-preserving rollback. Prefer a forward fix or temporarily stop affected authentication traffic.

## Main-session review

Integration review corrected missing access-token session linkage, late OpenIddict rejection that did not produce the required authentication failure, stale permission aliases, unconnected registered logout metadata, stale-context persistence, and worker batch starvation caused by front-channel-only sessions. Regression tests exercise actual retained credentials as well as persistence rollback and retry behavior.

The review also corrected CSP replacement so unrelated directives and non-iframe pages retain their original policy. The implementation was divided among three GPT-5.6 Terra agents; integration review, corrections, and final verification were performed in the main session.

## Verification — 8 September 2026

The solution build passed with zero errors and the existing `ASPIRE010` CLI-bundle warning. All 1,836 tests in the relevant suites passed, with none skipped:

| Suite | Passed |
| --- | ---: |
| Domain | 449 |
| Application | 466 |
| Infrastructure | 407 |
| API integration and controller tests | 371 |
| API unit tests | 77 |
| Public contracts | 60 |
| Architecture | 6 |

The eight credential sequence tests obtain real codes, access tokens, refresh tokens, and cookies, terminate their session through the API, and exercise retained credentials. They cover revoke-one/all, reset, disable followed by enable, physical session deletion, local logout, OIDC confirmation and antiforgery, unaffected sessions, machine credentials, and privilege removal. Persistence tests cover audited idempotence, excluded-session epochs, rollback after a partial save, stale-context writes, and due-delivery selection. Notification tests cover cancellation followed by a new worker, backoff, completion persistence, and repeated logout.

`dotnet ef migrations has-pending-model-changes` reported no pending changes. The strict MkDocs build and local secrets scans passed. No production database migration, external RP delivery, browser E2E suite, or external OIDC certification suite was run; these results verify the implementation and in-process integration rather than a deployed conformance certification.
