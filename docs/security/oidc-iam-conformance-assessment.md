# OpenIdentityStack: OIDC conformance and IAM security assessment

Assessment date: **5 September 2026**

Assessed commit: **`8de63f288420830ec61f2e472c80e97d2335de93`**

Method: source review, official specification/library research, and execution of existing backend tests.

Scope: OpenID Provider/authorization server, upstream federation client, Admin API resource server, Management Web, and supporting credential/session operations.

## Executive assessment

**OpenIdentityStack has a substantial, tested OpenID Connect foundation, but the reviewed implementation has material security gaps and cannot be described as fully conformant or production-hardened on this evidence.** OpenIddict supplies much of the protocol machinery. The most serious findings concern application decisions around that machinery: account linking, disabled users, resource audiences, authorization, consent, and session termination.

The assessment records **9 High and 8 Medium findings**. No Critical finding was established. High does not mean an unauthenticated internet attacker can exploit every finding: each entry states its prerequisites. Several concern a registered client, an existing credential, a configured upstream provider, or database access. Severity is qualitative, not a CVSS score.

The solution build succeeded, and **1,355 existing tests passed** across five backend suites. Passing these tests does not resolve the findings: some tests intentionally assert behavior that conflicts with the desired security policy. No exploit against a deployed service was attempted, and no new adversarial regression tests were added.

The repository targets **Basic OP, authorization code, PKCE, discovery, and static registration**. Its documentation describes hosted OIDF runs and warning/manual-review handling, but the actual exported results and completed certification evidence were not inspected. This report is **not an OpenID certification**. The OIDF certification process is separate from repository testing and this review. [Declared scope][E-scope], [OIDF certification][S-cert].

## Solution and trust boundaries

| Component | Observed responsibility and behavior |
| --- | --- |
| Backend | .NET 10; Domain/Application/Infrastructure layers; ASP.NET Core API and Razor login pages. OpenIddict packages are pinned to **7.6.1**. PostgreSQL/EF Core persist identities, applications, OpenIddict records, sessions, and audit data. [Packages][E-packages], [composition][E-program] |
| OpenID Provider / authorization server | Code, refresh-token, and client-credentials grants. `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/introspect`, `/connect/revoke`, `/connect/logout`, discovery, and JWKS. Public code clients require PKCE; accepted challenges are S256 only. [Server setup][E-setup], [application validation][E-app] |
| Local authentication | Framework password hashing, custom user-status/password-policy checks, cookie sign-in, and application session creation. Administrators can have a local-password fallback when federation is the default. [Account controller][E-account], [credential validation][E-credentials] |
| Upstream federation | Active configured providers are registered with ASP.NET Core OIDC middleware using code flow. The callback identifies users by provider/subject and can provision or link users. This is ordinary upstream OIDC federation, **not implementation of the separately named OpenID Federation specification**. [Middleware][E-external], [JIT provisioning][E-jit] |
| Client registration | Administrator-managed Applications model, projected to OpenIddict registrations: grants, scopes, redirect URIs, client type, credentials, PKCE, and consent setting. Dynamic registration is outside the declared scope. [Projection][E-projection] |
| Tokens and claims | Signed ID tokens; production access-token encryption remains enabled. User/profile claims are projected by custom code. User roles and permissions are also added to access tokens. OpenIddict token storage is enabled; local validation checks token and authorization records. [Projection][E-claims], [setup][E-setup] |
| Admin API | Bearer authentication and named permission policies protect management endpoints. It consumes the same issuer's access tokens; audience separation and the role-name shortcut are findings below. [Policy implementation][E-policy] |
| Management Web | React/Vite public OIDC client using `oidc-client-ts` (manifest range `^3.5.0`), code flow, sessionStorage, and `/api/me` for permissions. It treats access tokens as opaque rather than decoding production encrypted tokens. [Browser authentication][E-web], [design decision][E-opaque] |
| Sessions and operations | A domain `UserSession` is separate from OpenIddict token/authorization records and RP cookies. Cookie sign-out, domain revocation, token revocation, and downstream logout therefore need explicit coordination. Data Protection keys are persisted to the database. [Session revocation][E-revoke], [composition][E-program] |

The principal trust boundaries are: upstream assertions → local identity; browser cookie → authorization grant; client registration → permitted claims/resources; bearer token → Admin API permission; and database/key storage → credential integrity. The findings focus on failures to maintain those boundaries.

## Baseline and interpretation

The protocol baseline is **OIDC Core 1.0 errata set 2**, **Discovery 1.0 errata set 2**, **OAuth 2.0 RFC 6749**, **Bearer Tokens RFC 6750**, **PKCE RFC 7636**, and **OAuth Security BCP RFC 9700**. JWT validation is compared with **RFC 8725**. Revocation, introspection, and implemented logout/session features are assessed against their respective specifications. [Core][S-core], [Discovery][S-discovery], [OAuth][S-oauth], [Bearer][S-bearer], [PKCE][S-pkce], [BCP][S-bcp], [JWT BCP][S-jwt].

Security guidance comes from OWASP and **NIST SP 800-63B-4**. NIST is an explicit comparison target here, not an assertion of a legal obligation or of a claimed AAL/FAL certification. MFA is not mandatory for Basic OIDC protocol conformance, but its absence matters greatly for an administrative IAM service. [NIST][S-nist].

Status meanings:

- **Supported:** inspected implementation and relevant tests support the stated control; not a blanket specification pass.
- **Partial/gap:** a concrete control is missing, inconsistent, or bypassed.
- **Unverified:** evidence is insufficient, especially for deployment or full protocol sequences.
- **Outside scope:** optional capability not claimed; absence alone is not nonconformance.

Risk meanings: **Critical** = practical broad issuer/privileged compromise with minimal prerequisites; **High** = plausible takeover, privilege escalation, or persistent unauthorized access; **Medium** = conditional exposure, weakened protection, privacy loss, or security-relevant interoperability failure; **Low** = limited operational/interoperability impact; **Informational** = capability or evidence note. Confidence describes evidence quality independently of severity.

## Conformance and control matrix

| Area | Assessment | Evidence and qualification |
| --- | --- | --- |
| Code flow and grant selection | **Supported** | Code, refresh, and confidential client-credentials flows are configured; password and implicit grants are not enabled. This is consistent with the modern BCP posture. [E-setup][E-setup] |
| PKCE | **Supported** | Public code clients cannot disable it; projection registers the requirement; server removes `plain`. Existing tests include S256 discovery and rejection of plain challenges. Confidential clients can omit PKCE for certification compatibility; use it for normal confidential deployments too. [E-app][E-app], [E-projection][E-projection], [preflight][E-preflight] |
| Discovery and JWKS | **Supported in tested configuration** | Preflight tests verify configured issuer/endpoint URLs, code-only response types, public subjects, RS256, claims/scopes, S256-only metadata, and absence of RSA private-key fields. Production host/proxy behavior remains unverified. [E-preflight][E-preflight] |
| Redirect, code/client binding, protocol errors | **Supported with assurance limits** | OpenIddict validation remains enabled. Custom authorization-error handling is designed to redirect only to validated destinations. Existing code-flow/error tests passed; comprehensive malicious URI/replay cases still require OIDF evidence. [Error middleware][E-errors], [code-flow tests][E-code-tests] |
| `prompt=none`, `prompt=login`, `max_age`, `auth_time` | **Partial** | Implemented and tested in direct scenarios. Preservation of original authentication time through sliding cookies/refresh needs additional verification; see U1. [E-authorize][E-authorize] |
| Consent and offline access | **Gap — F05** | `RequireConsent` is stored, but authorization does not enforce approval. Prearranged first-party consent can be valid; that does not explain ignoring an explicit requirement. |
| ID token / UserInfo claims | **Partial — F09** | Standard profile/email/address/phone release is scope/request-gated; internal state claims are trimmed. `email_verified` provenance is incorrect. Returning requested identity claims in ID tokens also has a documented privacy tradeoff. [Claims][E-claims], [warning justification][E-warnings] |
| Resource restriction and administrative authorization | **Gap — F03/F04** | No Admin API audience configuration; permissions are not confined to the receiving resource; `admin` role name bypasses concrete permissions. |
| Refresh replay protection | **Framework-supported; runtime edge cases unverified** | OpenIddict 7.6.1 defaults to rolling refresh tokens and replay handling; no disabling override was found. Defaults include a 30-second reuse leeway and 14-day refresh lifetime. Test leeway/concurrent reuse separately from stale-user/session behavior in F06. [Pinned options][S-oi-options], [replay handling][S-oi-replay] |
| RFC 7009 revocation | **Supported framework endpoint; lifecycle integration incomplete** | Endpoint and persisted-token validation exist. Revoking domain sessions does not revoke those records. A token revocation endpoint is not proof of global logout. [S-revocation][S-revocation], [E-setup][E-setup], F06 |
| RFC 7662 introspection | **Supported with assurance limits** | Built-in client/activity validation is retained, and custom permission output is caller-filtered. Full authorization, inactive-response, caching, and propagation scenarios were not independently exercised against production. [Introspection handler][E-introspection], [S-introspection][S-introspection] |
| RP/front/back-channel logout | **Partial — F10/F12** | Signed logout-token construction is good; normal client-session registration does not connect notification delivery, front-channel frames are not rendered, and hintless logout lacks confirmation. These profiles are deferred for certification. |
| OIDC Session Management | **Gap — F11** | `check_session_iframe` is advertised but denied embedding by global headers. |
| Local and federated authentication | **Partial — F01/F02/F07/F13/F14** | Local password/status checks and middleware validation exist; account linking, federation status enforcement, MFA, password policy, and enumeration need remediation. |
| TLS, cookies, CORS, proxies | **Supported configuration controls** | Production HTTPS/HSTS, secure HttpOnly login cookies, explicit CORS origins, and trusted-proxy configuration exist. This does not verify the deployed TLS endpoint or network configuration. [E-program][E-program], [proxy configuration][E-proxy] |
| Key persistence and audit | **Partial — F08/F16/F17** | Production signing/encryption certificates are required; Data Protection persistence lacks key wrapping; development logs expose headers; session actions lack durable actor audit. |
| FAPI, JAR/PAR, dynamic registration, implicit/hybrid, OpenID Federation | **Outside declared certification scope** | Do not score every omitted extension as a Core defect. No FAPI, mTLS sender-constraint, DPoP, AAL2, or OpenID Federation conformance is established by this assessment. [E-scope][E-scope] |

## High-risk findings

### F01 — Upstream email automatically links to an existing local account

**High · high confidence · identity trust failure.** The callback extracts email without verification evidence. JIT provisioning then looks up that email and permanently links the upstream subject to the existing user, without proof of control of the local account. An existing application test explicitly expects this linking behavior. [Callback, lines 190–202][E-account-email], [JIT, lines 67–94][E-jit-link], [existing test][E-jit-test].

An attacker needs an account at a configured provider that can assert the victim's email—for example, a self-editable/unverified email claim or an address reassigned at that provider. The victim may be an administrator. This is not an arbitrary unsigned-token attack: upstream authentication can be cryptographically valid while the linking decision is unsafe. OIDC identifies users by issuer/subject; email is not a stable unique identity key. [Core §5.7][S-claim-stability].

**Remediation and acceptance:** require authenticated, explicit linking to the existing account, or a narrowly defined enterprise provisioning policy with authoritative identifiers. Simply requiring `email_verified=true` is insufficient for unrestricted cross-provider linking. Tests must show that a new provider subject with an existing email cannot acquire the local account, including verified-email and recycled-address cases.

### F02 — Federated authentication can sign in locally disabled users

**High · high confidence · account lifecycle bypass.** The external callback retrieves/provisions a user and signs in without `CanAuthenticate()`/status enforcement. Upstream-identity lookup does not filter account status; session creation checks existence only. Local password login does enforce disabled/pending state, so the two paths disagree. [Callback, lines 220–287][E-account-federated], [lookup][E-upstream-lookup], [session creation][E-create-session], [local checks][E-credentials].

A disabled user who retains a valid linked upstream identity can establish a fresh local session. Existing authorization code issuance also does not reject a disabled persisted user. Disabling an IAM account therefore does not reliably close the federation path. This is an authorization/lifecycle failure, not a requirement that OIDC use a particular authentication method. [OWASP authorization][S-authorization].

**Remediation and acceptance:** enforce active user and currently permitted provider state immediately before sign-in/token issuance. Test disabled and pending linked accounts through the actual external callback and subsequent authorization flow; both must fail without creating a usable session.

### F03 — Admin API accepts local tokens without an administrative audience boundary

**High · high confidence · OAuth resource-isolation gap.** Local validation enables token/authorization record checks but never configures expected audiences. OpenIddict 7.6.1 skips audience validation when that set is empty. At issuance, all effective user permissions are projected into access tokens rather than being limited to a resource/client; Admin API policies trust those claims. [Validation configuration][E-validation], [claim projection][E-claims], [policy][E-policy], [version-pinned audience validator][S-oi-audience].

A client or resource holding a valid locally issued access token for a privileged user can forward it to the Admin API even if it was intended for another resource. Production encryption hides claims from the holder but does not prevent replay of the opaque bearer credential. Expected audience validation and resource-restricted privileges are separate controls; both are needed. [RFC 9700 §2.3][S-bcp-resource], [RFC 8725 §3.9][S-jwt-audience].

**Remediation and acceptance:** define and enforce an Admin API audience, and filter scope/permission release by client/resource authorization. A wrong-audience token with otherwise valid signature and admin claims, and a token with no audience, must fail at the resource boundary. Run positive tests for the management client separately.

### F04 — Role name `admin` silently grants every platform permission

**High · high confidence · least-privilege bypass.** The permission handler converts a case-insensitive `admin` role claim into `Permissions.All`, regardless of its permission list. Ordinary role creation accepts the name without reserving this privilege. API unit tests explicitly assert universal access for `admin`/`ADMIN`; these tests passed. [Policy, lines 52–56][E-admin-role], [role creation][E-role], [tests][E-admin-role-test].

A user assigned a role named `admin` can perform actions absent from its configured permissions. The prerequisite is assignment of that issuer-controlled role, not merely submitting an arbitrary request header. This undermines a permission-based operating model and can survive attempts to reduce that role's explicit permissions. [OWASP least privilege][S-authorization].

**Remediation and acceptance:** remove role-name authorization shortcuts and derive access from explicitly granted permissions. Audit existing `admin` assignments before rollout. A role named `admin` with only read permission must fail write/delete operations; changing a display/name label must not change authority.

### F05 — Explicit consent configuration is not enforced

**High · high confidence · protocol/application policy gap.** Application projection records `RequireConsent=true` as explicit consent, but the authorization controller copies requested scopes and signs in without checking consent type, stored approval, denial, or `prompt=consent`. It handles login/none prompts only. [Consent projection][E-consent-setting], [authorization, lines 209–226][E-consent-issuance].

A registered client can obtain its permitted claims/tokens from an existing login even when the operator configured user consent. If it is also allowed refresh tokens, offline access needs a valid consent basis. Prearranged consent can be legitimate for controlled first-party relationships; there is no such policy evaluation in the reviewed path to justify bypassing an explicit setting. [Core consent and offline access][S-consent], [S-offline][S-offline], [OpenIddict application responsibilities][S-oi-consent].

**Remediation and acceptance:** implement approval/denial and durable authorization records, scope-expansion handling, and `consent_required` for silent requests needing interaction. Test initial consent, denial, repeated approved access, changed scopes, and silent offline access. Any first-party exemption must be explicit and independently tested.

### F06 — Session revocation, password reset, and changed privileges do not consistently invalidate credentials

**High · high confidence in source paths; full attack sequences unexecuted · credential lifecycle gap.** Domain session revocation updates `UserSession` only. OpenIddict validation checks different token/authorization records. No production consumer for session-revoked/password-changed/user-disabled events was found. Password reset and user disable save and audit without revoking credentials. Refresh projection copies the old principal, including obsolete roles/permissions. [Revoke][E-revoke], [reset][E-reset], [disable][E-disable], [exchange][E-exchange], [existing-principal projection][E-refresh-claims].

There are additional fail-open paths: login still creates a cookie after unsuccessful session creation; code/refresh exchange accepts absent/malformed session IDs and explicitly tolerates a missing session in all environments. Local form logout clears cookies without terminating the domain session. The main cookie has no domain-session validation hook. [Cookie creation][E-account-session], [exchange, lines 317–345][E-exchange], [local logout][E-local-logout], [cookie configuration][E-program].

An attacker holding an old access token can retain access after domain-session revocation until token expiry. Role removal can remain ineffective for refreshed tokens while the domain session remains valid. Password reset does not establish eviction of stolen credentials. These are distinct from a successful RFC 7009 request against a particular token. [OWASP session invalidation][S-session], [RFC 7009][S-revocation].

**Remediation and acceptance:** define an atomic or reliably delivered invalidation contract across user, cookie, domain session, OpenIddict grant/token, and RP sessions. Fail closed if session creation/lookup fails, refresh current user/authorization facts, and use appropriate token errors. Test retained access token, refresh token, OP cookie, and RP cookie after logout, revoke-all, session deletion, password reset, user disable, and role removal; state the allowed propagation deadline.

### F07 — Privileged local login has no enforced MFA

**High · high confidence · authentication assurance gap.** Local login issues the authenticated cookie immediately after password verification. MFA fields exist in the user model, but no enrollment/challenge/passkey/TOTP/recovery enforcement path was found in production code. Local administrator fallback can remain available even with an MFA-protected upstream provider. [Password sign-in][E-local-signin], [MFA model][E-mfa], [fallback policy][E-credentials].

A compromised administrator password is sufficient for the local route while enabled. A stored `MfaEnabled` field is not evidence of enforcement. This is a strong IAM hardening concern and does not meet the MFA aspect of an AAL2 target; it is not by itself an OIDC Core violation. [NIST AAL2][S-nist-aal2].

**Remediation and acceptance:** require phishing-resistant MFA for administrators, enforce step-up for sensitive credential/security changes, and define controlled recovery/break-glass access. Verify local fallback cannot bypass the configured assurance requirement, including an account whose MFA flag is already enabled.

### F08 — Database-persisted Data Protection keys have no application-level wrapping

**High, conditional on database/key-table read access · high confidence · key custody gap.** The API persists Data Protection keys to the database and configures no `ProtectKeysWith*` mechanism. Microsoft documents that explicit persistence disables default key encryption at rest. These keys protect login cookies and are distinct from the OpenIddict signing/encryption certificates. [Configuration, lines 57–59][E-data-protection], [Microsoft key encryption guidance][S-dp].

Reading the key table or a backup can expose material useful for decrypting/forging authentication cookies, expanding the impact of database-read compromise. Deployed database/volume encryption and access controls were not examined; they can reduce exposure but do not provide separation from a principal that can read decrypted database rows.

**Remediation and acceptance:** wrap persisted keys with a separately controlled certificate or KMS key. Verify persisted XML does not contain unwrapped key material, an attacker with database access alone cannot use it, and legitimate instances still interoperate through restart, key rotation, and restore.

### F09 — `email_verified=true` is issued without verification provenance

**High when RPs rely on this assertion for account linking/access · high confidence · direct claim-semantics gap.** Both email projection branches hardcode true. A federated user can be created from an upstream email with `email_verified=false` or no verification claim, yet the local OP reports it as verified. Account status is not separate evidence that this address was affirmatively verified. [Claim construction, lines 54–62][E-email-verified], [federated creation][E-jit].

This can mislead downstream systems that use a verified-email assertion as part of their trust decisions, even after F01's local linking is fixed. OIDC gives `email_verified=true` an affirmative-verification meaning. [Core §5.1][S-standard-claims].

**Remediation and acceptance:** track address verification and provenance independently of account activation; trust upstream evidence only under a defined provider policy. Test upstream false/missing assertions and administrative activation: none should become true without qualifying evidence. Test address changes invalidate previous verification.

## Medium-risk findings

### F10 — Normal logins do not connect downstream logout delivery

**Medium · high confidence · optional logout profiles partially implemented.** Authorization attaches only session/client IDs. `AddClientSessionCommand` ignores its optional logout URIs, and `SetLogoutUris` has no production caller. The notifier skips null URIs. Normal login therefore does not create the data needed to notify RPs. Additionally, front-channel logout redirects immediately or returns JSON instead of rendering iframes; the active URL generator sends `sid` without `iss`, while an unused alternate generator hardcodes the issuer. [Session attachment][E-add-client], [notifier][E-notifier], [controller][E-logout], [alternate generator][E-front].

An RP session can remain authenticated after OP logout where users expect global sign-out. The signed back-channel token factory is a positive control, but does not establish delivery. [Front-channel specification][S-front], [back-channel specification][S-back].

**Acceptance:** provision logout metadata, perform real authorization and RP session creation, then verify registered back-channel POSTs and rendered front-channel requests carry correct issuer/session values before final redirect. Test RP outages and retries. Until complete, describe the limitation to integrators.

### F11 — Advertised session-check iframe is blocked by the OP's own headers

**Medium · high confidence · advertised extension mismatch.** Discovery always publishes `/connect/check_session`, while global `X-Frame-Options: DENY` prevents RP embedding. The iframe script also lacks explicit registered-client/expected-origin rejection; it does bind its hash/reply to the message origin, which is a useful but incomplete control. [Metadata][E-session-metadata], [headers][E-headers], [iframe][E-iframe].

**Acceptance:** apply an endpoint-specific framing policy and validate expected origins/client bindings. Run a browser test with two distinct origins and an attacker origin. Include third-party-cookie restrictions in the supported-browser policy. The JavaScript-readable random session-state cookie is intentional for this extension; it should not be confused with the HttpOnly authentication cookie. [Session Management][S-session-spec].

### F12 — Logout without `id_token_hint` signs out without confirmation

**Medium · high confidence · RP-Initiated Logout gap.** Anonymous GET/POST `/connect/logout` immediately clears cookies without a confirmation decision. An attacker can navigate a logged-in browser there, producing forced logout. The logout specification requires asking the user when the hint is absent or does not match the current session. [Controller, lines 51–72][E-logout], [RP-Initiated Logout §2][S-rp-logout].

**Acceptance:** obtain confirmation for the required cases and reject forged/mismatched hints appropriately. This finding does **not** assert an open redirect or acceptance of invalid hints: OpenIddict's pre-controller validation remains active and must be tested through the full pipeline.

### F13 — Password defenses need policy, throttling, and hash-upgrade improvements

**Medium · high confidence · NIST/OWASP alignment gap.** The policy requires 12 characters and composition rules, with no compromised/common-password blocklist. Production login throttling is five attempts per IP per five minutes; no per-account failure control was found. Distributed attempts can evade that partition. For the current single-factor route, compare with NIST's 15-character minimum, blocklist, and account-oriented rate limiting. [Policy][E-password-policy], [rate limits][E-rates], [NIST password guidance][S-nist-password].

The hasher uses framework defaults and collapses `SuccessRehashNeeded` to success without updating the stored hash. Current framework source uses PBKDF2-HMAC-SHA512 with 100,000 iterations; OWASP currently lists 220,000 for that variant. Random salts and fixed-time verification are strengths, but older hashes can remain indefinitely. [Wrapper][E-hasher], [framework source][S-hasher], [options][S-hasher-options], [OWASP storage][S-password-storage].

**Acceptance:** benchmark stronger configured hashing, rehash successful legacy logins, support long passphrases, block compromised choices, and add abuse-resistant account throttling. Verify distributed guesses are limited without enabling trivial permanent account denial of service.

### F14 — Anonymous local-fallback check reveals privileged account eligibility

**Medium · high confidence · enumeration.** `/Account/CanAccessLocalLogin` returns whether an arbitrary supplied email belongs to an eligible administrator when external-default/local-fallback configuration applies. It is unauthenticated and has no dedicated limiter. Distinct local-auth-denied errors and missing-user timing are additional signals. [Endpoint, lines 397–440][E-enumeration], [credential validation][E-credentials].

**Acceptance:** remove the public privilege probe or make its response independent of account existence/privilege. Normalize authentication errors and adequately equalize computational paths. Test known privileged, known unprivileged, and nonexistent accounts under each fallback setting. [OWASP authentication responses][S-authentication].

### F15 — Administrative bearer credentials are exposed to same-origin JavaScript

**Medium · high confidence in exposure, no XSS exploit demonstrated · browser architecture risk.** Management Web stores the OIDC user and access token in sessionStorage. Same-origin malicious JavaScript can read them. SessionStorage limits persistence compared with localStorage but is not an HttpOnly security boundary. [Browser storage][E-web], [OWASP Web Storage guidance][S-session].

**Acceptance:** prefer server-side token custody/BFF with appropriately protected cookies for administration, or explicitly accept the SPA exposure and document compensating controls. Test token lifetime, cleanup/logout, CSP, dependency controls, and injected-script consequences. Do not claim token encryption prevents bearer replay.

### F16 — Development request logging records complete credentials

**Medium, environment-specific · high confidence · secret exposure.** Development middleware logs every header value, including Authorization and Cookie, at Information level. A later truncated authorization log does not redact the earlier full values. [Program, lines 174–187][E-header-logging].

Real credentials used against a Development instance can enter logs and telemetry. The production guard limits scope; a production deployment mislabeled Development would have greater impact. Similarly, the `api`-scope-to-all-permissions shortcut is development/testing guarded, not an unconditional production grant. [Development grant][E-dev-grant].

**Acceptance:** remove whole-header logging, allowlist non-sensitive fields, and verify logs with canary credential values. Preserve useful correlation without storing reusable credentials. [OWASP data exclusion][S-logging].

### F17 — Session revocation and logout lack durable actor audit coverage

**Medium · high confidence in reviewed paths · detection/accountability gap.** Session-revoke, revoke-all, and process-logout use cases do not write through the existing audit service. A domain event with no consumer is not durable evidence of who acted, on what, and with what outcome. Some user/credential operations do audit, so this is incomplete coverage rather than absence of auditing everywhere. [Revocation][E-revoke], [logout processing][E-process-logout], [audit service][E-audit].

**Acceptance:** record actor, target, outcome, time, and safe correlation for security-relevant session actions. Test success/failure attribution and storage failure behavior. Avoid token/cookie values. Broader cross-write/audit atomicity is already identified as deferred work in repository documentation. [OWASP logging][S-logging], [deferred work][E-deferred].

## Unverified risks and capability limits

These are not additional confirmed vulnerabilities and are excluded from the finding counts.

| ID | Open question / provisional risk | Evidence and decisive verification |
| --- | --- | --- |
| U1 | **Authentication freshness — provisional Medium** | `GetAuthenticationTime` prefers ticket `IssuedUtc` over original `auth_time`; cookies slide. Ticket renewal may therefore make an old authentication appear fresh. Test original login → sliding renewal → `max_age` challenge → code/refresh exchange and compare original authentication time. Existing numeric-claim/direct-controller tests do not establish preservation. [Helper][E-auth-time], [cookie configuration][E-program] |
| U2 | **Production deployment and key rotation — risk not rated without deployment evidence** | Certificates are required, but only one configured signing and one encryption certificate are loaded. Verify overlapping rollover, old-token acceptance/decryption, JWKS refresh, fixed issuer, proxy trust, TLS configuration, secret delivery, and backup restoration in the real topology. Do not equate certificate loading with a complete rotation procedure. [E-setup][E-setup] |
| U3 | **mTLS / asymmetric client authentication — unverified capability** | Certificate thumbprint matching exists, but `CertificateAuthenticationTests` explicitly contains utility tests rather than full mTLS integration. Transport proof-of-possession, client auth metadata, certificate expiry/trust, and token binding need dedicated testing. Certificate registration alone is not RFC 8705 sender-constrained-token conformance. [Handler][E-client-auth], [tests][E-certificate-tests], [RFC 8705][S-mtls] |
| U4 | **Refresh replay and revocation — residual assurance gap** | Exercise code reuse, verifier/client mismatch, refresh rotation/reuse inside and outside leeway, token-client revocation binding, and immediate resource rejection. Test real PostgreSQL and production encrypted-token settings; the API suite uses in-process SQLite and Testing configuration. [Fixture][E-fixture] |
| U5 | **Browser controls and public profile privacy — review required** | API CSP permits inline scripts; separately hosted Management Web headers were not verified. `/profiles/{preferredUsername}` exposes active-user display/profile data without an explicit publication-consent check in that endpoint. Establish intended publication policy and test browser/server boundaries rather than calling this a demonstrated XSS exploit. [Headers][E-headers], [public profiles][E-public-profile] |
| U6 | **Credential recovery and assurance profiles — incomplete evidence** | Administrative password reset exists, but no complete end-user verified-email/recovery-token lifecycle was established. No AAL2/AAL3 or federation assurance-level claim is supported. Choose target assurance and provisioning/recovery workflows before scoring additional profile requirements. [Reset][E-reset], [NIST][S-nist], [NIST federation][S-nist-federation] |
| U7 | **Certification and dependency security — not established** | Repository guidance reports prior suite behavior; exported logs, manual screenshots, exact tested deployment, and completed certification were not inspected. An official-site search found no product result, which is not proof of non-certification. No package vulnerability/SCA scan was performed. Obtain signed-off evidence tied to the release/profile. [Certification workbench][E-suite], [OIDF][S-cert] |

## Recommended remediation order and release evidence

1. **Close identity and privilege boundaries:** F01–F04 and F09. Block unintended linking, enforce local account state, restrict Admin API audiences/claims, and remove role-name privilege shortcuts. Review existing links and role assignments for affected accounts.
2. **Make credential termination dependable:** F06, followed by F10–F12 and F17. Define what logout, reset, disable, and revoke mean for every credential type. Add adversarial sequence tests before implementation changes.
3. **Enforce authentication and consent policy:** F05/F07/F13/F14. Decide administrator MFA, break-glass, approved first-party consent, password/recovery, and throttling policies; then enforce them consistently.
4. **Harden key and browser exposure:** F08/F15/F16 plus U2/U5. Validate key wrapping and rollout/restore behavior, remove credential logging, and decide the administrative browser token architecture.
5. **Produce release-bound conformance evidence:** run the full selected OIDF plans after remediation, including manual-review modules, with reviewed warnings and exported evidence. Add profile-specific logout testing only when those capabilities are complete and claimed.

Treat the High findings as release decisions requiring a fix or a documented, demonstrably effective deployment constraint. A deferral label or passing test count is not a compensating control. For every fix, retain a negative test that fails on the assessed behavior, a positive interoperability test, and the operational rollback implications.

## Verification performed and limitations

| Command / check | Result in this assessment |
| --- | --- |
| `git rev-parse HEAD` and working-tree inspection | Commit identified above; working tree initially clean apart from research notes created during this task. |
| `dotnet restore OpenIdentityStack.slnx` | Passed after permitted escalation to access NuGet configuration. Initial sandbox attempt could not read the user NuGet configuration. |
| `dotnet build OpenIdentityStack.slnx --no-restore` | Passed, zero errors, one `ASPIRE010` warning about the disabled Aspire CLI bundle. |
| Infrastructure test module | **395 passed**, zero failed/skipped. |
| Application test module | **467 passed**, zero failed/skipped. |
| API unit test module | **74 passed**, zero failed/skipped. |
| API test module | **359 passed**, zero failed/skipped; includes OIDC preflight and authentication tests. |
| Contract test module | **60 passed**, zero failed/skipped. |
| Test execution form | `dotnet test --test-modules 'tests/**/bin/Debug/net10.0/*<Suite>.dll' --no-build --no-restore`; API/contract runs also used `--max-parallel-test-modules 1`. |
| Strict documentation build | **Passed** with the repository's pinned documentation dependencies in a temporary folder; existing informational notices concerned unrelated pages/links. |
| Report integrity | All reference-style citations resolve to definitions; all commit-pinned repository paths and line anchors were checked against this checkout. The initial claim that `git diff --check` passed was incorrect: the metadata used trailing spaces for Markdown line breaks. Review follow-up removed those spaces and verified the corrected design diff. External URLs were researched, not exhaustively link-crawled. |

The earlier no-restore attempts found no test project/assets in the fresh checkout; restore/build resolved that prerequisite. Test counts above are from the subsequent successful runs. Domain, architecture, frontend Vitest, browser E2E, live OIDF plans, and production infrastructure tests were **not run** for this assessment. Passing API tests in Testing mode does not validate production TLS, certificate deployment, enforced production rate limits, encrypted-token integration, or external RP delivery. Existing tests are evidence for their assertions, not independent proof that the security requirements are correct.

This assessment did not include a secrets scan, penetration test, formal threat model, full ASVS audit, or supply-chain assessment. No production code or security behavior was changed.

## Sources and reproducible code evidence

Code links below are pinned to the assessed commit. File/line evidence is authoritative for what was inspected; standards and framework links establish the comparison baseline. Public references were researched on 5 September 2026, including Context7 queries for OpenIddict/ASP.NET Core responsibilities and primary-source verification.

[S-core]: https://openid.net/specs/openid-connect-core-1_0.html
[S-discovery]: https://openid.net/specs/openid-connect-discovery-1_0.html
[S-oauth]: https://www.rfc-editor.org/rfc/rfc6749.html
[S-bearer]: https://www.rfc-editor.org/rfc/rfc6750.html
[S-pkce]: https://www.rfc-editor.org/rfc/rfc7636.html
[S-bcp]: https://www.rfc-editor.org/rfc/rfc9700.html
[S-bcp-resource]: https://www.rfc-editor.org/rfc/rfc9700.html#section-2.3
[S-jwt]: https://www.rfc-editor.org/rfc/rfc8725.html
[S-jwt-audience]: https://www.rfc-editor.org/rfc/rfc8725.html#section-3.9
[S-revocation]: https://www.rfc-editor.org/rfc/rfc7009.html
[S-introspection]: https://www.rfc-editor.org/rfc/rfc7662.html
[S-mtls]: https://www.rfc-editor.org/rfc/rfc8705.html
[S-standard-claims]: https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
[S-claim-stability]: https://openid.net/specs/openid-connect-core-1_0.html#ClaimStability
[S-consent]: https://openid.net/specs/openid-connect-core-1_0.html#Consent
[S-offline]: https://openid.net/specs/openid-connect-core-1_0.html#OfflineAccess
[S-rp-logout]: https://openid.net/specs/openid-connect-rpinitiated-1_0.html#RPLogout
[S-front]: https://openid.net/specs/openid-connect-frontchannel-1_0.html
[S-back]: https://openid.net/specs/openid-connect-backchannel-1_0.html
[S-session-spec]: https://openid.net/specs/openid-connect-session-1_0.html
[S-cert]: https://openid.net/certification/
[S-nist]: https://pages.nist.gov/800-63-4/sp800-63b.html
[S-nist-aal2]: https://pages.nist.gov/800-63-4/sp800-63b.html#aal2
[S-nist-password]: https://pages.nist.gov/800-63-4/sp800-63b.html#passwordver
[S-nist-federation]: https://pages.nist.gov/800-63-4/sp800-63c.html
[S-authorization]: https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html
[S-authentication]: https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html#authentication-responses
[S-session]: https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html
[S-logging]: https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html
[S-password-storage]: https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html#pbkdf2
[S-dp]: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0
[S-hasher]: https://source.dot.net/Microsoft.Extensions.Identity.Core/PasswordHasher.cs.html
[S-hasher-options]: https://source.dot.net/Microsoft.Extensions.Identity.Core/PasswordHasherOptions.cs.html
[S-oi-audience]: https://github.com/openiddict/openiddict-core/blob/7.6.1/src/OpenIddict.Validation/OpenIddictValidationHandlers.Protection.cs#L690
[S-oi-options]: https://github.com/openiddict/openiddict-core/blob/7.6.1/src/OpenIddict.Server/OpenIddictServerOptions.cs
[S-oi-replay]: https://github.com/openiddict/openiddict-core/blob/7.6.1/src/OpenIddict.Server/OpenIddictServerHandlers.Protection.cs#L1100
[S-oi-consent]: https://documentation.openiddict.com/configuration/authorization-storage.html

[E-packages]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/Directory.Packages.props
[E-program]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Program.cs
[E-setup]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/OpenIddictSetup.cs
[E-validation]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/OpenIddictSetup.cs#L205
[E-app]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Domain/Applications/Application.cs#L501
[E-account]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AccountController.cs
[E-account-email]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AccountController.cs#L190
[E-account-federated]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AccountController.cs#L220
[E-account-session]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AccountController.cs#L264
[E-local-signin]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AccountController.cs#L313
[E-local-logout]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AccountController.cs#L372
[E-enumeration]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AccountController.cs#L397
[E-credentials]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Application/Users/Commands/ValidateUserCredentialsUseCase.cs
[E-external]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/ExternalAuthenticationSetup.cs#L77
[E-jit]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Application/Federation/Commands/JitProvisionUserUseCase.cs
[E-jit-link]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Application/Federation/Commands/JitProvisionUserUseCase.cs#L67
[E-jit-test]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/tests/OpenIdentityStack.Application.Tests/Federation/JitProvisionUserUseCaseTests.cs#L143
[E-projection]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/OpenIddictApplicationProjection.cs#L140
[E-consent-setting]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/OpenIddictApplicationProjection.cs#L159
[E-claims]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/TokenClaimProjectionService.cs
[E-email-verified]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/TokenClaimProjectionService.cs#L54
[E-refresh-claims]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/TokenClaimProjectionService.cs#L117
[E-policy]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authorization/RequirePermissionAttribute.cs
[E-admin-role]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authorization/RequirePermissionAttribute.cs#L52
[E-admin-role-test]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/tests/OpenIdentityStack.Api.UnitTests/Authorization/PermissionRequirementTests.cs#L94
[E-role]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Domain/Roles/Role.cs#L103
[E-web]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.ManagementWeb/src/lib/auth.tsx#L17
[E-opaque]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/docs/adr/0004-management-web-opaque-access-token-permissions.md
[E-revoke]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Application/Sessions/Commands/RevokeSessionCommand.cs#L57
[E-reset]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Application/Users/Commands/ResetPasswordUseCase.cs#L50
[E-disable]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Application/Users/Commands/DisableUserUseCase.cs#L34
[E-exchange]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs#L317
[E-authorize]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs#L88
[E-consent-issuance]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs#L209
[E-auth-time]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs#L593
[E-mfa]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Domain/Users/User.cs#L124
[E-data-protection]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Program.cs#L57
[E-header-logging]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Program.cs#L174
[E-dev-grant]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/AuthorizationController.cs#L260
[E-upstream-lookup]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Persistence/Users/UserRepository.cs#L154
[E-create-session]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Application/Sessions/Commands/CreateSessionCommand.cs#L67
[E-add-client]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Application/Sessions/Commands/AddClientSessionCommand.cs#L54
[E-notifier]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/BackChannelLogoutNotifier.cs#L49
[E-logout]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Authentication/LogoutController.cs#L51
[E-front]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/FrontChannelLogoutService.cs#L43
[E-process-logout]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Application/Sessions/Commands/ProcessLogoutCommand.cs
[E-session-metadata]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/SessionManagementHandlers.cs#L53
[E-headers]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.ServiceDefaults/SecurityHeadersExtensions.cs#L35
[E-iframe]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Pages/Connect/CheckSession.cshtml#L44
[E-password-policy]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/PasswordPolicyValidator.cs#L12
[E-rates]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Configuration/RateLimitingConfiguration.cs#L23
[E-hasher]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/PasswordHasher.cs#L13
[E-audit]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Audit/AuditLogService.cs#L28
[E-proxy]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Configuration/ForwardedHeadersConfiguration.cs#L46
[E-introspection]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/IntrospectionPermissionsHandler.cs
[E-client-auth]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/ApplicationClientAuthenticationHandler.cs
[E-certificate-tests]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/tests/OpenIdentityStack.Api.Tests/Authentication/CertificateAuthenticationTests.cs#L18
[E-public-profile]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Api/Users/PublicProfilesApi.cs#L15
[E-errors]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/src/OpenIdentityStack.Infrastructure/Identity/AuthorizationErrorRedirectMiddleware.cs
[E-code-tests]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationCodeFlowTests.cs
[E-preflight]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/tests/OpenIdentityStack.Api.Tests/Authentication/OidcConformancePreflightTests.cs
[E-fixture]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/tests/OpenIdentityStack.Api.Tests/Fixtures/AppHostFixture.cs#L27
[E-scope]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/docs/certification/openid-connect-certification-scope.md
[E-suite]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/docs/certification/run-oidf-conformance-suite.md
[E-warnings]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/docs/certification/conformance-warning-justifications.md
[E-deferred]: https://github.com/Tjeerd-menno/open-identity-stack/blob/8de63f288420830ec61f2e472c80e97d2335de93/docs/reference/DEFERRED-BACKEND-REMEDIATION-ITEMS.md
