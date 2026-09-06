# Email verification evidence

An active account does not by itself establish ownership of its email address. The one-time independent-evidence migration retains the legacy verification invariant for existing non-pending users that have a non-empty local password hash: it records independent evidence for their current normalized email, timestamped from `ModifiedAt` or `CreatedAt`. Pending and federated-only accounts are not backfilled. Provider email trust defaults to off, including existing registrations after migration.

An operator with provider write permission can change **Trust email verification** in the provider Settings page. The dedicated policy endpoint is `PUT /api/admin/providers/{id}/email-verification-trust` with `{ "trusted": true }` or `false`. Each committed change has a durable `Provider.EmailVerificationTrustChanged` audit entry. Ordinary provider edits preserve this setting.

For this release, upstream evidence is accepted only from the validated ID token: `email_verified` must be true, its email must match the account's current address, and the provider must be explicitly trusted when the evidence is saved. Email supplied by UserInfo alone is not verification evidence. Provenance records the normalized address, provider registration, exact validated issuer, and observation time. Local verification records independent evidence without a provider. Account creation and account linking remain governed by their separate policies.

Operators with user read permission can inspect current verification and its source in the user profile and user detail API. New tokens, refreshed tokens, and UserInfo calculate `email_verified` from current persisted evidence. Historical token claims, role names, active status, and authentication cookies are not evidence. There is currently no supported email-change workflow; evidence is address-bound so a future workflow cannot carry verification to a different address.

Turning trust off withdraws that provider's evidence in the same transaction as the policy and audit change. Independent verification survives. Evidence remains available for investigation with its withdrawal time. A concurrency version prevents a login using stale provider trust from restoring evidence after withdrawal. A racing operation fails rather than overriding committed policy; retry it after reading current state.

The earlier `RecordIndependentEmailVerificationEvidence` migration creates the evidence table and performs that bounded local-password backfill. `RecordEmailVerificationEvidence` then extends the table with provider provenance and defaults every provider to untrusted. Its rollback refuses to drop the provenance columns while provider-derived evidence remains; use an approved forward migration to quarantine those rows before retrying. No seed routine should call email verification merely to activate an account. Use forward corrective migrations and preserve evidence backups once evidence has been recorded.

Withdrawal of evidence controls subsequent issuance and UserInfo. It does not revoke existing credentials, tokens, or sessions. Operators responding to a provider compromise must complete the [provider-compromise revocation procedure](#provider-compromise-revocation-procedure), including downstream services that validate tokens locally.

## Provider-compromise revocation procedure

1. Disable the provider first so it cannot create new sessions while the incident is contained.
2. Record the affected user IDs from the provider identity inventory before withdrawing trust. Preserve the provider, identity, and audit records needed for investigation.
3. Withdraw **Trust email verification** and confirm the `Provider.EmailVerificationTrustChanged` audit entry. This invalidates that provider's active email evidence but does not terminate credentials.
4. In Management Web, open **Sessions**, find every affected user, and revoke every active session. The operator needs `sessions:revoke`. Confirm no active session remains for those users.
5. Treat OAuth credentials separately. Have clients submit every known access or refresh token to `/connect/revoke`. For credentials the authorization server cannot enumerate or downstream APIs that validate self-contained tokens locally, deny the affected subject IDs at each resource until the maximum token lifetime has elapsed. Use an emergency signing-key rotation only when the incident's blast radius justifies invalidating all tokens.
6. Verify that fresh token and UserInfo responses do not report `email_verified: true` solely from the withdrawn provider. Review audit records and downstream denial logs before closing the incident.

Revoking a domain session alone is not proof that an already issued access token has stopped working. Keep the downstream deny in place until revocation or expiry has been verified at every resource server.

Verification covers the trusted/assertion matrix, provisioning independence, stale refresh claims, management API persistence and audit, source-specific withdrawal, relational stale-write rejection, and provider UI permissions. These checks are implementation evidence, not an OpenID certification claim.

New provider evidence is committed under a provider policy row lock, with the trusted policy version revalidated inside the same transaction. Evidence recording does not rotate the provider policy version; repeated evidence reads make no provider write. Trust withdrawal takes that lock before reading dependent users, so it includes evidence committed by a login it waited for. The PostgreSQL concurrency regressions exercise simultaneous users and both orders of login versus withdrawal.

Provider-wide trust withdrawal processes at most 100 user aggregates at a time, saves each batch inside the same transaction, and clears tracking before reading the next batch. The provider policy lock remains held throughout. A later failure rolls back earlier batches, policy changes, and transactional audit records; there are no per-batch commits or population-sized identifier lists.

Trust withdrawal seeks active evidence through the partial `(ProviderId, UserId)` index with a user-ID cursor. Each page contains at most 100 distinct users, and retired pages are never scanned again. Aggregate updates and audits remain in one transaction; keyset paging does not introduce partial commits.
