# Identity boundary cutover rehearsal

See the [implementation evidence](identity-boundary-implementation-evidence.md) for the twelve-ticket delivery, test results, resolved review findings and remaining release risks.

The readiness gate implements the release prerequisites in [ADR 0005](../adr/0005-identity-and-administrative-trust-boundaries.md). Implementing or testing this workflow does **not** authorize a production cutover. A successful rehearsal is evidence for an operator change decision, not formal OpenID Connect certification.

## Deployment order

1. Back up PostgreSQL, quarantine and verification evidence, signing/encryption keys, data-protection keys and configuration. Record the release commit, database migration history, emergency operator and consumer owners in a restricted change record. Do not include passwords or tokens.
2. In an isolated copy, apply all boundary migrations, including `GateIdentityBoundaryCutover`. Reconcile legacy client registrations and map every business scope to its own protected resource. Retain disabled resources in the token inventory: disabling issuance does not recall old JWTs.
3. Prepare the fixed Management Web public SPA with PKCE and the exact configured callback/logout URIs. The API and DbMigrator must receive the same `OpenIddict:Clients:ManagementWeb` URI configuration. The explicit bootstrap is off by default and does not restore a withdrawn grant. Approve other administrative integrations through the human workflow. Use fresh `ois.admin` tokens for the dedicated administrative audience; generic and combined audiences remain rejected.
4. Drain older serving binaries before enabling traffic on the complete boundary release. Mixed binaries cannot enforce the shared epoch, evidence revisions or current administrative authority. Verify database connectivity, readiness/health endpoints, signing and data-protection availability, local login availability, audit persistence and Management Web access on every serving instance.
5. Inventory identity associations through the paginated user/provider workflow and the cutover readiness screen. Every quarantined association blocks execution. A configured password is only a candidate, not proof of ownership; an emergency administrator cannot establish another user's ownership. Federation-only users without independent safe access require a separately specified proof-based recovery delivery. Do not delete links, fabricate issuer/evidence, reactivate disabled accounts or use a raw identifier to clear this gate.
6. Test each consumer with old and fresh credentials. Record its actual rejection behavior and residual acceptance window as described below. Keep the evidence reference in the change record and record the resource review through Management Web.
7. Sign in using the independently accessible emergency administrator's **local password**, then select **Verify my emergency access** within five minutes. The protected authentication/session marker comes from actual local authentication. The server verifies the current boundary, active persisted session, local login availability and current explicit unrestricted authority. It never treats a role named `admin`, a stored password hash or an operator-supplied user ID as proof. Existing explicit wildcard grants are retained.
8. Refresh readiness, resolve every blocker and save its audit snapshot. Execution requires explicit acknowledgement and repeats the persisted checks inside the same serializable transaction that advances the epoch and revokes credentials. Choose and retain one operation UUID. Execute only under a separately authorized production change.
9. Sign in again. Confirm old cookies, codes, refresh and access tokens fail at supported OP/local endpoints; verify fresh dedicated-audience Management Web access, current permissions, preserved local disablement, unchanged quarantines and consumer rejection within the reviewed window. Record operation counts, audits and observed results.

## What the gate proves

`GET /api/admin/security/cutover-readiness` requires current `users:read`, `applications:read` and `sessions:revoke` authority through an approved administrative client. It returns aggregate identity and verification evidence counts, client preparation, resource reviews, emergency session evidence and outstanding access-token metadata. `CredentialCutover.PreflightEvaluated` stores the snapshot. Repeated inventory is read-only with respect to users, links and credentials.

The emergency proof is tied to a specific current-epoch local-password session and actual authentication time. It expires after five minutes and is invalidated by disablement, session removal/revocation, loss of unrestricted authority or loss of local login availability. Credential refresh cannot refresh its authentication time. Any new epoch requires a new proof. This verifies access to the operator account; it is not identity-association evidence for other users.

The gate checks uncached persisted application, grant, role, membership and resource state. Management Web must have its reviewed deployment identity and delegated ceilings covering user/application inventory and session revocation. Active legacy client migration markers, unapproved clients requesting administrative scope and unmapped non-protocol scopes block the cutover. No approval or resource grant is created by preflight.

`POST /api/admin/security/emergency-access-evidence` accepts no user, issuer, subject or session identifiers. `PUT /api/admin/security/business-resources/{resourceId}/token-window-review` accepts a control mechanism, a nonnegative maximum residual window in seconds and an evidence reference. Both require fresh unrestricted human approval and acknowledgement. The review is bound to the exact resource revision and epoch. Configuration changes invalidate that review.

## External acceptance windows

OpenID Provider revocation does not recall JWTs at offline resource servers, ID tokens already consumed by relying parties, or independent relying-party sessions. For **each** consumer, test and record one of these controls:

- `OnlineIntrospection`: prove the consumer rejects inactive tokens and measure its maximum introspection cache, propagation and clock-skew window.
- `ConsumerRevocation`: prove its own denylist, credential epoch or session invalidation control, including replicas and caches; record the maximum remaining acceptance duration.
- `OfflineExpiry`: record the maximum access-token lifetime and clock skew, drain in-flight issuance, and wait for or explicitly accept the entire remaining window. Unknown-expiry token rows block this choice.

These are operator-recorded external rehearsal results, not automatic verification of a remote consumer. Use evidence references to measured results, deployment configurations and responsible owners; do not enter zero merely to clear a blocker. Account for in-flight issuance and tokens issued after a preliminary snapshot. If the residual duration cannot be bounded, stop the change until a consumer control exists.

If multiple reviews have the same latest timestamp, the gate requires a new review instead of choosing an operator decision by random record ID. Save one later review to resolve that ambiguity; earlier evidence remains retained.

The inventory recognizes both OpenIddict 7 URI-style access-token identifiers and legacy `access_token` rows. Direct database queries cannot rely on the token manager to map legacy hints; see the [OpenIddict 7 migration guide](https://documentation.openiddict.com/guides/migration/60-to-70.html). The PostgreSQL browser regression checks a real issued access credential against the inventory and confirms that a zero-second offline window remains blocked.

The server conservatively compares the offline window against all persisted, non-expired or unknown-expiry OP access-token rows, including revoked rows that an offline validator might still accept. Token rows do not provide a reliable historical resource index. Pruned rows, externally issued credentials and relying-party sessions are outside this database inventory and must be covered by the consumer's measured maximum lifetime/control. No readiness response claims they were recalled. A disabled resource still requires a review.

## Failure and rollback

An unresolved prerequisite returns HTTP 409. The transaction commits only `CredentialCutover.PreflightBlocked` with the snapshot; it does not change the boundary or revoke credentials. Resolve the prerequisite and retry. There is no quarantine override. If a database serialization conflict or revocation/audit failure occurs, the boundary, revocations and operation record roll back together. Inspect the operation record before retrying the **same** UUID. A completed operation returns its original result without invalidating fresh recovery credentials again.

Use `CredentialCutover.EmergencyAccessTested`, `CredentialCutover.ResourceWindowReviewed`, preflight events and `CredentialBoundary.Cutover` to correlate the change. Retain the operation ID, epoch, deployment/migration versions and external evidence. Restrict inventory and audit access; do not export secrets or tokens into reports.

Roll back by restoring service on a release that still enforces quarantine, credential/evidence revisions and the current epoch. Retain the durable evidence tables and all revocations. Never restore unsafe email linking, name-derived privilege or generic administrative audiences. Restoring a pre-cutover database or code that ignores the boundary requires isolation and a new invalidation plan before traffic resumes. If emergency access cannot be restored safely, stop; do not manufacture identity evidence.

## Repeatable verification evidence

The automated rehearsal uses separate isolated SQLite databases: a representative legacy fixture remains blocked, while a clean prepared fixture executes and recovers. It never deletes the legacy records to obtain a passing cutover. The local password/PKCE/code/token flow is real; resource consumer reviews in the fixture are explicitly simulated and do not establish production readiness.

| Evidence | Automated coverage |
| --- | --- |
| Quarantined federation-only and password-candidate users; disabled user retained; explicit wildcard retained; empty `admin` role unchanged; repeatable audited blocked preflight | `CredentialCutoverTests.LegacyRehearsalPreservesQuarantineAndDisablementDespiteTestedEmergencyLogin` |
| Actual emergency login, approved Management Web preparation, resource review validation, atomic cutover, stale access/code/refresh/cookie rejection, fresh recovery and idempotent retry | `CredentialCutoverTests.CutoverRequiresHumanApprovalRejectsOldCredentialsAndFreshLoginRecovers` |
| Fresh proof expiry, current role withdrawal, missing/revoked session, resource revision changes, unknown expiry including revoked token rows | `CredentialCutoverReadinessStoreTests` |
| Uncached administrative grant withdrawal and exact deployment identity; unmapped scopes and disabled resource inventory | `CredentialCutoverResourceInventoryTests` |
| Gate executes before any boundary or credential mutation; revocation failure rolls back | `CredentialBoundaryTests` |
| Human provenance required; invalid resource review rejected | `CredentialCutoverReadinessTests` |
| Generic/combined administrative audiences and ceilings; next-request authority withdrawal; provider trust withdrawal and stale issuance | `AdministrativeEntitlementTests`, `AdministrativeRequestAuthorizationTests`, `EmailTrustCredentialTests` in the integrated stack |
| Blocked UI execution, no raw identifiers in emergency proof, explicit acknowledgement and stable operation retry | `CutoverReadinessPage.test.tsx` and shared cutover client contract tests |

Run the focused .NET classes using `dotnet test --project tests/OpenIdentityStack.Api.Tests --filter-class '*CredentialCutoverTests'` and the corresponding Infrastructure/Application/Contract projects; run Management Web build, lint and Vitest and `python -m mkdocs build --strict`. Record exit codes and the exact integrated commit in the release record. The final release must also pass PostgreSQL/Aspire deployment and consumer rehearsals; SQLite results alone do not validate PostgreSQL locking or a production rollout. Open external-resource windows remain explicit release limitations until measured and approved.
