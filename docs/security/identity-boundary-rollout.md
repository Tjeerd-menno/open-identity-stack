# Identity and privilege boundary rollout

This delivery implements the policies in [ADR 0005](../adr/0005-identity-and-administrative-trust-boundaries.md). Each implementation layer must pass its focused checks before integration; the final cutover rehearsal remains a release gate.

## Account linking

An authenticated upstream identity with an email matching an existing local account is rejected instead of automatically linked. The public sign-in failure is generic. Operators can investigate the audited association denial without recording the supplied subject or email in that event.

The identifier-only administrative linking endpoint returns HTTP 403 Problem Details with code `Forbidden.UpstreamIdentity.ProofRequired`. Management Web no longer offers a form for supplying a provider and subject to link an account. Listing and unlinking existing associations remain available under their existing permissions. Proof-based linking and recovery require a separate workflow; matching email is insufficient proof.

New upstream identities with no email collision can receive ordinary local accounts only when the provider permits JIT provisioning. Disabling provisioning does not disable the provider or remove existing associations. Deploy the provider configuration migration before running the updated API. Existing provider registrations retain the previously effective enabled setting; explicit changes are persisted and enforced thereafter.

Before deploying this layer, identify integrations using identifier-only linking and preserve independently usable login methods for affected users. Restoring email-based linking or the raw-identifier bypass is not a safe rollback. Database uniqueness constraints continue to reject concurrent attempts to claim the same email or provider/subject; a failed attempt must not be treated as an existing-account authentication success.

## Delivery tracking

The checked-in OpenAPI contract marks raw linking as deprecated and describes its proof-required 403 response. The shared administrative client no longer exports `linkUserUpstreamIdentity` or its request type. This is an intentional breaking security change: remove raw-link calls from integrations; there is no supported replacement until an independent proof workflow is delivered. Identity listing remains supported.

The Admin API contract release is **2.0.0**, advancing from 1.1.0 for this removal of the advertised raw-link success response and shared-client operation. Release the API, shared administrative client, and Management Web together under [ADR 0003](../adr/0003-unified-release-train-for-breaking-api-changes.md). This contract release does not introduce a versioned URL: `/api/admin` remains the administrative base path. Consumers must migrate away from raw-link calls before upgrading; the endpoint continues to return the documented proof-required 403.

Implementation issues are [#445–#456](https://github.com/Tjeerd-menno/open-identity-stack/issues?q=is%3Aissue+is%3Aopen+label%3Aready-for-agent). Broader assessment findings and formal OpenID Connect certification are separate work. Production cutover is not authorized by implementing these changes.
