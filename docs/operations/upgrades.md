# Upgrades

Treat upgrades as coordinated changes across the migrator, API, management web, and deployment configuration.

## Recommended sequence

Use this sequence when the schema remains compatible with the currently running API. For `RemoveCredentialCutover`, follow the [maintenance-window sequence below](#removing-the-pre-release-credential-cutover-schema) instead: old API instances must stop before the migrator runs.

1. review release notes and image tags
2. back up PostgreSQL
3. update manifests or runtime configuration
4. run the database migrator
5. roll out the API
6. roll out the management web
7. verify health, sign-in, and at least one token flow

## Removing the pre-release credential cutover schema

Migration `20260908175924_RemoveCredentialCutover` drops `CredentialBoundary`, `CredentialCutovers`, `EmergencyAccessEvidence`, and `ResourceTokenWindowReviews`. Earlier API builds query these tables during cookie validation, token processing, and cutover operations. This migration requires downtime for existing development or pre-release installations; a rolling API update with the migrator running first is not supported.

1. Prepare the new API, migrator, and management web artifacts and back up PostgreSQL.
2. Put the installation into maintenance mode and stop routing new requests to every old API instance. Let in-flight requests finish, then stop all old API processes, replicas, and background workers that use this database. Prevent automatic restarts of the old deployment.
3. Confirm no old API instances are running before starting the new DbMigrator. Apply the migration while API traffic remains disabled.
4. Start only the new API build and deploy the matching management web. Verify health, fresh sign-in, a token flow, and session revocation before restoring traffic.

If migration or validation fails, keep traffic disabled and prefer a forward repair. Do not restart old API binaries against the upgraded schema. The migration's `Down` method recreates empty tables with an initial boundary value; it cannot restore the previous credential boundary or evidence. Returning to an old build requires a coordinated database recovery from the pre-migration backup and security review before serving traffic again.

Fresh installations have no old API instances to drain; run the migrator before starting the API as usual.

## Pay special attention to

- redirect URI changes for management web or integrated applications
- certificate or key location changes
- browser origin changes
- changes to the admin seed behavior

## Rollback posture

Before upgrading, decide:

- whether the database migration is reversible
- which image tags you can redeploy quickly
- how to validate that older clients still trust the authority metadata after rollback

## Local disablement and installation bootstrap

Federated callbacks, provisioning of an already linked account, authorization using an existing local cookie, and authorization-code/refresh exchanges check persisted local disablement. Public authentication errors do not disclose that an account is disabled. Re-enable an account through the authorized Users administration operation, which records `User.Enabled`; neither upstream success nor a migrator rerun reverses the decision.

All DbMigrator local-user paths (configured production administrator, development administrator, and certification users) are create-only. If the normalized email already exists, the migrator preserves its status, password, profile and role assignments. System-role seeding also preserves existing permission assignments. Remove obsolete `Seed__AdminUser__ResetPasswordOnExistingUser` and `Seed__Certification__ResetExistingUsers` settings: they no longer reset existing users. Use an authenticated recovery/password workflow and explicit privilege administration instead.

For a fresh installation with an empty user store, configure the bootstrap administrator credentials through the deployment secret mechanism and run the migrator after the system roles are created. Creating a new administrator requires the explicit all-permissions system role; a role whose permissions were reduced is not silently restored. Configured administration runs before optional development or certification users. Once any local user exists, changing the configured email cannot create another bootstrap administrator. The new account, role assignment and `User.BootstrapCreated` audit entry commit in one serializable transaction. Activation does not establish email-verification evidence. An existing email collision never converts an ordinary account into an administrator. Competing bootstrap attempts fail on a serialization or uniqueness conflict instead of merging identities; rerun after inspecting the resulting account.

Before rollout, test an independently accessible emergency administrator. A disabled bootstrap account remains disabled even if its password appears in seed configuration. If no authorized administrator remains, pause rollout and follow the reviewed recovery procedure; do not restore access by rerunning seeds or matching email.

The user-status concurrency token rejects a stale profile/provisioning save if another transaction committed disablement. Failed writes do not overwrite that decision; reload and reevaluate the operation instead of blindly retrying stale state. The associated migration updates EF metadata without changing database columns. All API instances must run the upgraded code for the check to apply; rolling back the binaries removes this protection and must be treated as a security rollback. Revocation of already issued resource tokens is handled by the separate credential-lifecycle work.
