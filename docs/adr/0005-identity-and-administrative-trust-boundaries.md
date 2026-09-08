# Explicit identity and administrative trust boundaries

Status: accepted.

To address F01–F04 and F09 in the [IAM assessment](../security/oidc-iam-conformance-assessment.md), account linking requires explicit proof of control of the existing local account rather than matching email, and local administrative disablement remains authoritative until explicitly reversed locally. Administrative access requires an explicit client entitlement as well as appropriate subject permissions; role names grant no authority, while a deliberately assigned all-permissions grant includes future permissions and is reserved for controlled bootstrap and emergency administration. Email-verification trust is explicitly configured per provider, defaults to untrusted, and preserves verification provenance independently of account activation and account-linking authority.

## Consequences

These policies were accepted during the design interview on 5 September 2026; they describe the intended boundaries, not completed implementation. They trade automatic account merging and broadly reusable administrative credentials for explicit trust decisions, while retaining controlled full-administration access. Existing links, client registrations, and verification claims need a migration policy; an active account alone cannot justify a verified-email backfill, and ordinary seed/provisioning reruns must not silently reverse local administrative disablement.

## Client and resource separation

Client applications, protected resources, and permission namespaces have distinct identities and explicit relationships. The Admin API gets a dedicated resource identity and scope, separate from the shared business `api` scope. Delegated access is the intersection of the user's permissions, the client's permission ceiling, and the requested resource; machine-to-machine access uses explicitly assigned application permissions without inheriting human privileges.

## Initial delivery and privilege governance

The first security release rejects a new upstream identity that collides with an existing local email, with a generic public error and an auditable diagnostic. Proof-based linking is a separate delivery slice; the existing endpoint accepting administrator-supplied provider/subject identifiers is not an alternative identity-proof mechanism. No enterprise automatic-linking exception has been accepted.

Initially, only holders of the explicit all-permissions grant may approve administrative clients, expand their administrative permission ceilings, or grant unrestricted platform access. These operations require fresh authentication, explicit acknowledgement, and durable audit; ordinary application/role editing does not authorize them.

An upstream issuer is immutable once identities are linked. Replacing it requires a new provider registration and an explicit identity migration; neither matching email nor matching subject text across issuers transfers access.

## Authentication and token boundaries

Administrative client approval, expansion of administrative permission ceilings, and unrestricted grants require actual human authentication within five minutes, bound to the acting administrator. Cookie renewal and token refresh do not establish freshness; missing trustworthy authentication time requires reauthentication. Machine credentials cannot perform these human approval operations.

Administrative access tokens target only the Admin API. Missing, incorrect, or combined administrative/business audiences are rejected; clients needing business access obtain a separate token.

Where provisioning is enabled, a newly authenticated upstream identity may receive an ordinary local account without trusted email-verification evidence, provided no email collision exists. Identity derives from the provider and subject; email verification remains false or unknown, and no administrative privilege is assigned automatically. Unverified email provides no basis for linking or email-dependent authorization.

## Recovery and cutover constraints

Proof-based identity recovery is deferred unless the legacy-link inventory makes it a prerequisite for safe migration; the raw provider/subject linking bypass is closed. Rollout requires a tested, independently accessible emergency administrator. Neither routine seeding nor recovery shortcuts may silently reactivate a disabled account or manufacture email-verification evidence.

Administrative client registrations are prepared before cutover: Management Web is explicitly configured and other administrative integrations require approval. The upgraded Admin API has no compatibility bypass for generic `api` tokens; it requires fresh tokens with the dedicated audience and permission ceilings. Existing explicit wildcard grants are preserved, but role names such as `admin` are never automatically converted into wildcard grants.

Existing identity links require independent evidence of legitimate association before they are trusted. Links without that evidence are retained as quarantined records and cannot authenticate a user; signing in through the disputed link does not prove control of the local account. Before rollout, inventory affected users and independently usable login methods. Users relying solely on quarantined federation links cannot migrate until safe recovery is available, which may bring the deferred proof-based recovery slice forward.

Cutover invalidates pre-cutover OpenID Provider sessions and grants and requires fresh authentication; the Admin API rejects pre-cutover administrative credentials. Independently validating business APIs need a separate plan for outstanding tokens: changing the provider cannot instantly recall tokens those APIs continue to accept.

## Withdrawal of trust and authority

Withdrawing a provider's email-verification trust invalidates evidence derived solely from that provider while retaining its provenance for audit. Independent verification evidence can remain valid. Newly issued tokens reflect the change, and affected credentials carrying the withdrawn assertion are invalidated through supported mechanisms, with the independent-resource limitation above explicitly accounted for.

Changes to a user's administrative permissions, a client's administrative approval, or its permission ceiling take effect on the next Admin API request after commit. Authorization must consult current authority or use dependable invalidation; waiting for an existing access token to expire is insufficient. This accepts the targeted credential-lifecycle dependency from F06 needed to enforce these boundaries, without bringing all broader session and logout remediation into this slice.

## Verification expectations

Plan regression tests before implementation for email collisions and quarantined links, disabled-user federation, issuer replacement, verification provenance and trust withdrawal, administrative audience isolation, delegated and machine permission limits, protected approval operations, and authority withdrawal using already-issued credentials. Cutover validation must demonstrate emergency access, rejection of pre-cutover credentials, and safe handling of users without an independent login method. These are delivery requirements; the design interview does not establish that the current implementation passes them.
