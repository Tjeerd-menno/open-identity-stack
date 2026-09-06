# Provider email-trust withdrawal

An operator with `providers:write` withdraws trust through the provider Settings page or `PUT /api/admin/providers/{id}/email-verification-trust` with `{ "trusted": false }`. The operation returns 204 only after the trust policy, evidence withdrawal, credential invalidation, and durable audit commit together.

## Scope and immediate effect

Evidence from this provider retains its address, issuer, observation time, and withdrawal time. Independent local evidence or another trusted provider can still support the same address. Re-enabling provider trust does not restore withdrawn evidence: a new validated assertion must establish new evidence.

When withdrawal removes the last sufficient evidence for any affected address, including a historical address, all OP tokens and authorizations for that user are revoked through the supported OpenIddict managers. Active local sessions are marked revoked. Users with sufficient independent evidence for every affected address retain their credentials. Revocation is deliberately scoped to the user because an outstanding credential may contain the older address.

Client-credentials access tokens carry an OP-issued `ois.subject_kind=application` marker. Their token records also retain this trusted issuance classification. User-specific withdrawal skips only these application token records, even when a client's identifier is identical to a user's UUID; it still revokes user tokens and authorizations. Revision validation accepts the machine classification only with one marker, a matching `sub` and `client_id`, and no user revision. Group claims cannot supply this reserved namespace. Ambiguous legacy credentials without the marker remain subject to the conservative user revision check. Global credential cutover still invalidates machine and user credentials alike.

A persisted `CredentialRevision` changes with this invalidation. User token projection captures it as `ois_credential_revision`; refresh preserves the captured value. The OP server and its local API validation both read the current revision from the database, without trusting cached token or user records. A token produced from an old projection after the revocation query still fails validation. Legacy credentials without this claim are eligible only while the user's revision has never advanced; the separate credential cutover invalidates the pre-release population globally.

`EmailEvidenceRevision` serializes evidence changes on each user. This prevents simultaneous withdrawals from different providers from each relying on the other's evidence and both leaving stale credentials usable. Provider trust's existing concurrency version also prevents stale login assertions from restoring withdrawn evidence.

Cookie-backed authorization validates persisted session state before issuing credentials. Session activity saves update only the properties changed on a tracked session, so a request that loaded an active session before withdrawal cannot overwrite a committed revocation. Fresh independent authentication may create a new session with corrected claims.

## Failure, retry, and audit

All changes use one scoped `OpenIdentityStackDbContext` and relational transaction, including the EF-backed OpenIddict managers and audit service. Cancellation, storage errors, concurrency conflicts, or audit failure abort the operation. Do not treat a failed HTTP request as proof of a committed withdrawal; read the provider policy and audit history, then retry with a fresh request. A retry after a successful commit does not restore evidence, change the original withdrawal time, or revoke newly issued credentials again.

`Provider.EmailVerificationTrustChanged` records the policy change. Each affected user also has `Provider.EmailTrustCredentialsRevoked` containing the provider ID and counts of revoked tokens, authorizations, and sessions. Audit details contain no email address or token material. The management switch reflects persisted policy and stays available for retry after an error.

## Independently validating business APIs

OpenIddict's [token storage and revocation documentation](https://documentation.openiddict.com/configuration/token-storage.html) distinguishes stored token revocation from the validation performed by a resource server. This deployment already enables token-entry and authorization-entry validation for local APIs; the revision check adds protection against stale concurrent issuance. Both OP validation paths reject stale revisions after the withdrawal commits. Requests already authorized before the commit cannot be recalled.

An external API that only verifies a JWT signature cannot observe this database change. Before withdrawal, inventory each relying party and record its actual mechanism and residual window:

| Consumer | Mechanism to observe withdrawal | Residual window to record |
| --- | --- | --- |
| OP endpoints and locally validated APIs | Stored revocation plus current user revision | Requests already authorized before commit |
| Remote API using OP introspection | Introspection reports revoked or stale credentials inactive; disable stale positive caching | Positive-cache TTL and requests already in progress |
| Remote API validating JWTs offline | Consumer-managed denylist or online revision/introspection check; otherwise expiry | Maximum remaining `exp` of affected tokens plus accepted clock skew and cache delay |
| Relying party using an ID token to establish its own session | Relying-party session termination or reauthentication policy | Its own session lifetime; ID-token expiry alone does not terminate that session |

Do not claim immediate global recall. If the remaining window exceeds the system's risk tolerance, stop affected access at the resource server or deploy an online check before treating withdrawal as effective there. Record the measured token expiries, clock skew, positive-cache TTLs, relying-party session lifetimes, and the responsible operator in the deployment record. Blanket signing-key rotation is not a substitute for a consumer inventory or session termination policy.

## Deployment and rollback

Apply `InvalidateWithdrawnEmailTrustCredentials` after the evidence migration, then deploy the projection, OP validation hooks, and credential/session cutover together on every serving instance. The migration adds two UUID revisions with an empty legacy baseline; it fabricates no email evidence. Drain older instances before enabling this administrative operation. Their validation code cannot enforce the revision boundary. Credentials invalidated by an older evidence-only release must be included in the global cutover.

Retain the revisions and evidence on rollback. Reverting to code that ignores them can accept stale signed credentials. Prefer a forward repair, or stop issuance and dependent access until compatible validation is restored. Downgrading the schema drops the revisions and is not a safe security rollback.

Verification covers relational commit/rollback/retry, multiple-source concurrency, old-address evidence, all stored OP credential types, real authorization-code and refresh flows, cached and late-issued credential states, corrected fresh issuance, management authorization, and UI failure handling. This is implementation evidence, not an OpenID certification claim.
