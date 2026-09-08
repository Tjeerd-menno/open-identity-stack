# Credential termination: baseline implementation research

This is an archived baseline. See [implementation and rollout](credential-termination-implementation.md) for the subsequent changes.

Date: 8 September 2026. Checkout: `8100/open-identity-stack`, commit `7abf59bc7951433ea821497ab919bd8aa4e4fbd5`.

Input: recommendation 2 of the assessment in the `de8e` checkout, dated 5 September 2026 and assessed at `8de63f288420830ec61f2e472c80e97d2335de93`: F06 first, then F10–F12 and F17. This research examines the current **8100 checkout**, not uncommitted implementation work in other checkouts.

**Baseline finding before implementation: recommendation 2 remained outstanding.** Domain session state, issuer cookies, OpenIddict credentials, and relying-party sessions do not share a dependable termination contract. Existing controls reject some revoked-session refreshes, but several paths bypass those controls. Source comparison found no changes from the assessed commit in the authentication controllers, session use cases, identity infrastructure, reset/disable use cases, cookie configuration, session endpoints, or the highlighted controller/session tests.

Method: source and test inspection, baseline Git comparison, and current OpenIddict documentation via Context7 and the official documentation website. Required Sonar secrets scans of the assessment, `src`, and `tests` passed outside the sandbox. **No builds, tests, deployed attacks, or propagation measurements were run.** Consequences below are source-supported inferences, not newly reproduced exploits.

## Existing behavior by operation

| Operation | What the code actually does | Remaining credential exposure |
| --- | --- | --- |
| Revoke one session (`DELETE /api/admin/sessions/{id}`) | Marks the domain session revoked and saves it. The DELETE is a logical revocation, not physical deletion. | No explicit OpenIddict token/grant invalidation or RP notification. A well-formed session-linked code/refresh exchange is rejected; existing access tokens and OP cookies are not checked against this domain state. [Revoke use case][revoke], [endpoint][endpoints], [exchange][exchange], [cookie][cookie] |
| Revoke all user sessions | Iterates active sessions, optionally excluding one, saving each independently. | Same token/cookie/RP gaps; partial completion is possible if a later save fails. There is no enclosing transaction in this use case. [Revoke-all][revoke-all], [repository][repository] |
| Password reset | Changes the password hash, saves the user, then writes an audit event. | Does not terminate existing sessions, tokens, or cookies. [Reset][reset] |
| Disable user | Changes user status, saves, then audits. | Does not revoke credentials. The external callback and authorization/token issuance paths do not enforce current user status consistently. [Disable][disable], [callback][callback], [authorize][authorize], [exchange][exchange] |
| Remove a role | Removes the assignment, saves, then audits. | Access-token claims remain unchanged; refresh copies old role/permission/group claims. New authorization computes effective roles again, which does not repair already issued credentials. [Role removal][role], [authorize][authorize], [projection][projection] |
| Local form logout (`POST /Account/Logout`) | Clears the current browser's OP authentication cookie and session-management cookie. | Does not terminate the domain session or token records; a retained copy of the authentication cookie has no server-side domain-session validation hook. [Form logout][form-logout], [cookie][cookie] |
| OIDC logout / administrative logout | Uses a separate domain logout path and attempts downstream notification. | Token invalidation, RP registration/delivery, confirmation, and audit remain incomplete; see the [logout research](credential-termination-logout-research.md). |

These statements concern user credentials. Machine/client credentials need a separate scope and policy; requiring a user `sid` for client-credentials grants would be incorrect.

## F06: exact breaks in the lifecycle

1. **The revocation target and validation target differ.** `RevokeSessionUseCase` only updates `UserSession`. Local OpenIddict validation already enables token-entry and authorization-entry checks, but these read OpenIddict records. Searches across production `src` found no `IOpenIddictTokenManager`/`IOpenIddictAuthorizationManager` usage or revocation bridge for these operations. Session/user domain events are raised, but no production consumers of `SessionRevokedEvent`, `UserPasswordChanged`, or `UserDisabled`, or domain-event dispatch path, were found. [Revoke][revoke], [validation setup][validation], [session event][session-event], [user events][user-events]
2. **Failed session creation still produces a cookie.** Both local login and the external callback add `sid` only when session creation returns success, then sign in regardless of that result. This finding concerns returned failures; an uncaught exception would interrupt execution. Session creation checks user existence, not eligibility to authenticate. [Local login][login], [callback][callback], [creation][creation]
3. **Session validation fails open at code/refresh exchange.** Missing or malformed session IDs skip validation. A lookup returning exactly `Session not found` is accepted in all environments despite a development-oriented comment. Revoked, logged-out, and expired sessions are rejected when lookup occurs, but the endpoint returns `access_denied` rather than a token-endpoint `invalid_grant` for this branch. The validator checks neither current user status nor ownership of the session by the token subject. [Exchange][exchange], [validator][validator]
4. **Authorization trusts the cookie without validating domain session/user status.** It retrieves a persisted user but does not reject missing/disabled users here. Client-session registration results are ignored. The main cookie enables sliding expiration but has no `ValidatePrincipal` callback; a repository-wide search found no alternative principal validation hook. [Authorize][authorize], [cookie][cookie]
5. **Refresh preserves obsolete authority.** `ProjectExistingPrincipal` copies claims except legacy `session_id` and `auth_time`, preserving role, permission, and group claims without querying current user/authorization facts. The controller does not reload them at exchange. [Projection][projection], [exchange][exchange]
6. **Missing records can undermine termination.** Physically removing a session row produces the specially tolerated lookup result. This is an additional deletion/cleanup acceptance case; the public session DELETE endpoint itself only marks revoked. [Exchange][exchange], [endpoints][endpoints]

OpenIddict already provides useful building blocks: stored tokens can be revoked regardless of token format, and per-request token/authorization-entry checks can enforce revocation at colocated APIs. External resource servers need an explicit enforcement arrangement, such as introspection, with caching/propagation behavior defined. These framework controls do not automatically make an application-owned session row authoritative. [Token storage](https://documentation.openiddict.com/configuration/token-storage.html), [Authorization storage](https://documentation.openiddict.com/configuration/authorization-storage.html).

RFC 7009 defines invalidation of the submitted token and, where applicable, related grants/tokens; having that endpoint is distinct from connecting account lifecycle operations to invalidation. It also acknowledges propagation delays, which should be minimized. [RFC 7009](https://datatracker.ietf.org/doc/html/rfc7009#section-2.1).

## Tests: useful coverage and misleading assurance

| Evidence | What it establishes |
| --- | --- |
| `Exchange_RefreshToken_WithInvalidSession_ReturnsForbid` | A mocked invalid session yields a controller forbid result. It does not exercise a real issued refresh token or retained access token. [Test][invalid-test] |
| `Exchange_RefreshToken_WithoutSessionClaim_ReturnsSignIn` | Explicitly asserts the fail-open behavior and that validation is never called. Its expected outcome must change as part of remediation. [Test][missing-test] |
| `Login_Post_WhenSessionCreationFails_StillSignsIn` and `...DoesNotIncludeSessionIdClaim` | Explicitly preserve successful authentication after a returned session-creation failure. [Tests][login-test] |
| `RevokedSession_RefreshTokenFails` | Despite its name, only creates a session, revokes it, fetches it, and asserts `status == Revoked`. It never obtains or exchanges a refresh token. [Test][misnamed-test] |
| Application session validation/revoke/reset tests | Useful checks of individual state transitions; insufficient evidence for eviction across real credentials. See `tests/OpenIdentityStack.Application.Tests/Sessions/` and `tests/OpenIdentityStack.Application.Tests/Users/ResetPasswordUseCaseTests.cs`. |

## Recommended contract and implementation order

The following is a proposal, not existing behavior or a committed design.

1. **Specify scope and deadlines, then add sequence tests.** Distinguish current-session logout, revoke-one, revoke-all, password reset, disable, and privilege reduction. Decide whether offline grants survive ordinary logout; define that explicitly rather than inheriting accidental behavior. Compromise-driven reset/disable should terminate all affected user credentials. Set separate measurable deadlines for issuer/local API enforcement and remote RP enforcement. Do not claim immediate remote logout without receiver-side evidence.
2. **Make issuance and cookie use fail closed.** Require an active existing user and valid owned session for user grants; reject missing/malformed/deleted sessions. Do not issue a cookie when session creation fails. Validate existing cookies against authoritative state. Use typed failure reasons and protocol-appropriate errors without disclosing internal state.
3. **Connect termination to credentials.** Introduce an Application port with an Infrastructure implementation that coordinates domain state with OpenIddict grants/tokens. Persist enough session-to-grant linkage to target one session without accidentally revoking unrelated sessions or durable consent. Choose transactional local invalidation or a durable invalidation mechanism with bounded enforcement; merely raising currently unconsumed domain events is insufficient. Cover concurrent issuance and revocation.
4. **Refresh current authorization.** Reload user eligibility, effective roles, permissions, and groups at code/refresh exchange. Preserve granted-scope/resource constraints and original authentication facts; do not widen permissions because the client asks again. Existing access tokens still need an explicit revocation/version policy when privileges shrink.
5. **Finish downstream logout and audit.** Correct client metadata capture and front-channel rendering; make RP delivery durable, resumable, and idempotent. Commit actor/target/reason/outcome audit with local termination or durable work. Distinguish local termination from completed RP delivery. See [detailed follow-up findings](credential-termination-logout-research.md).

Minimum sequence suite: obtain an access token, refresh token, unredeemed authorization code, OP cookie, and test RP session; retain copies; perform each termination action; reuse every credential at its actual endpoint. Add missing/malformed/deleted/mismatched session IDs, failure to create/save sessions, disable concurrent with issuance, role/group reduction before refresh, another unaffected session, repeated termination, RP outage, cancellation, and process restart. Verify access-token rejection at the API, correct token errors, no silent reauthentication using an invalidated OP cookie, RP eviction within the agreed deadline, and complete audit outcomes. Add positive controls for unaffected users/clients and explicitly allowed offline behavior.

Rollout needs a policy for old cookies/tokens lacking session linkage, data backfill or forced reauthentication, coordination across application instances, and invalidation/delivery observability. Rollback must not reactivate credentials already terminated.

[revoke]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Application/Sessions/Commands/RevokeSessionCommand.cs#L53
[revoke-all]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Application/Sessions/Commands/RevokeAllUserSessionsCommand.cs#L60
[repository]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Infrastructure/Persistence/Sessions/SessionRepository.cs#L64
[endpoints]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Api/Sessions/SessionsApi.cs#L126
[reset]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Application/Users/Commands/ResetPasswordUseCase.cs#L50
[disable]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Application/Users/Commands/DisableUserUseCase.cs#L31
[role]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Application/Roles/Commands/UnassignRoleUseCase.cs#L71
[callback]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Api/Authentication/AccountController.cs#L220
[login]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Api/Authentication/AccountController.cs#L333
[form-logout]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Api/Authentication/AccountController.cs#L372
[cookie]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Api/Program.cs#L64
[authorize]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs#L145
[exchange]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs#L301
[projection]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Api/Authentication/TokenClaimProjectionService.cs#L116
[validation]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Infrastructure/Identity/OpenIddictSetup.cs#L204
[validator]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Application/Sessions/Queries/ValidateSessionQuery.cs#L53
[creation]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Application/Sessions/Commands/CreateSessionCommand.cs#L63
[session-event]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Domain/Sessions/UserSession.cs#L133
[user-events]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/src/OpenIdentityStack.Domain/Users/User.cs#L313
[invalid-test]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationControllerTests.cs#L1113
[missing-test]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationControllerTests.cs#L1194
[login-test]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/tests/OpenIdentityStack.Api.Tests/Authentication/AccountControllerTests.cs#L464
[misnamed-test]: https://github.com/Tjeerd-menno/open-identity-stack/blob/7abf59bc7951433ea821497ab919bd8aa4e4fbd5/tests/OpenIdentityStack.Api.Tests/Admin/SessionManagementTests.cs#L144
