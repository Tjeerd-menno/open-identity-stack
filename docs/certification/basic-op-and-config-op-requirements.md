# What Basic OP and Config OP certification actually require

Research answer for issue [#305](https://github.com/Tjeerd-menno/open-identity-stack/issues/305).
This document defines "green" for the OIDF conformance run.

## Sources

Every claim below is traced to one of these. Where a source does not settle a question, the
document says so explicitly rather than inferring.

| Ref | Source |
| --- | --- |
| `[SUITE]` | OpenID Foundation conformance suite, `https://gitlab.com/openid/conformance-suite`, `master` @ `daf33d61b982d5d33d134b07e9a36f76176b3eff` (2026-07-29). Java source read directly. |
| `[OP-TEST]` | OpenID Foundation, "Conformance Testing for OpenID Connect OPs", <https://openid.net/certification/connect_op_testing/> |
| `[CERT-LIST]` | OpenID Foundation, "Certified OpenID Connect Implementations", <https://openid.net/certification/certified-openid-connect-implementations/> |
| `[OIDDICT-SAMPLES]` | OpenIddict, `openiddict/openiddict-samples` (`dev` branch): repository `README.md` "Certification" section and the `samples/Contruum/Contruum.Server` sample. |
| `[PROFILES-PDF]` | OpenID Foundation, "OpenID Connect Conformance Profiles", <https://openid.net/wordpress-content/uploads/2018/06/OpenID-Connect-Conformance-Profiles.pdf> — **could not be machine-read** in this environment (no PDF rasteriser). Cited only indirectly, via the suite source comments that reference it. |

The suite source is the operative authority: the certification submission is a set of results
produced by the suite, so what the suite's Java code does *is* the requirement. The profiles PDF is
the historical definition the plan authors were matching, and the suite plan classes quote its table
rows in comments.

---

## 1. Plan identifiers and variant / configuration choices

### 1.1 Plan identifiers

Both identifiers in the issue are correct. `[SUITE]`

| Plan id | Display name | Certification profile name |
| --- | --- | --- |
| `oidcc-basic-certification-test-plan` | "OpenID Connect Core: Basic Certification Profile Authorization server test" | `Basic OP` |
| `oidcc-config-certification-test-plan` | "OpenID Connect Core: Config Certification Profile Authorization server test " (trailing space is in the source) | `Config OP` |

Source files: `src/main/java/net/openid/conformance/openid/OIDCCBasicTestPlan.java` and
`.../OIDCCConfigTestPlan.java`. Both are declared with `profile = TestPlan.ProfileNames.optest` and
`specFamily = TestPlan.SpecFamilyNames.oidcc`, i.e. they appear under **"Test an OpenID Provider"**
in the suite UI. `[SUITE]`

### 1.2 Variants the tester must choose

The suite offers a variant selector only for variant parameters the plan does **not** pin.

**Basic OP** pins three and leaves two open: `[SUITE]`

| Variant parameter | Pinned by the plan? | Value(s) |
| --- | --- | --- |
| `response_type` | pinned | `code` |
| `client_auth_type` | pinned | `client_secret_basic` for all modules except `oidcc-server-client-secret-post`, which is pinned to `client_secret_post` |
| `response_mode` | pinned | `default` |
| `server_metadata` | **tester chooses** | `discovery` or `static` — choose **`discovery`** |
| `client_registration` | **tester chooses** | `static_client` or `dynamic_client` — choose **`static_client`** |

The plan's own comment is explicit about why both client auth methods appear: "*the certification
profile requires that both basic and post are tested, but doesn't dictate which variant the other
tests are run with*". `[SUITE]`

**Config OP** pins everything; there is nothing to choose: `[SUITE]`

| Variant parameter | Value |
| --- | --- |
| `server_metadata` | `discovery` (pinned) |
| `client_registration` | `static_client` (pinned) |

### 1.3 Configuration fields the plan asks for

Derived from the `@VariantConfigurationFields` annotations reachable from the selected variants.
`[SUITE]`

For **Basic OP** with `server_metadata=discovery` + `client_registration=static_client`:

- `alias` — the tester-chosen alias, forms part of the redirect URI. `[OP-TEST]`
- `server.discoveryUrl`
- `client.client_id`, `client.client_secret`
- `client2.client_id`, `client2.client_secret` (from `AbstractOIDCCMultipleClient`)
- `client_secret_post.client_id`, `client_secret_post.client_secret` (from `OIDCCServerTestClientSecretPost`)
- `server.login_hint` — optional; defaults to `buffy@<issuer hostname>` if omitted
- `server.acr_values` — **only offered when `server_metadata=static`**; with discovery the suite uses
  the OP's advertised `acr_values_supported`, or falls back to `1` and `2`

For **Config OP**: `server.discoveryUrl` (plus the plan-level alias). The Config plan's single module
declares no client-specific configuration fields.

> **Uncertainty.** The suite stores **one** configuration JSON per plan
> (`TestPlanService.createTestPlan(..., JsonObject config, ...)`, and
> `DBTestPlanService.getModuleConfig` returns that same plan-level config for every module). So all
> 3 client entries above live in one config document; there is no per-module config. `[SUITE]`

---

## 2. Full test list, and what is required

### 2.1 Certification pass criteria

> "Certification of a profile requires that you have 'PASSED', 'REVIEW', 'WARNING' or 'SKIPPED'
> results for all tests in the profile. You cannot certify with any FAILED or INTERRUPTED results."
> `[OP-TEST]`

So the *whole* plan is required — there is no "optional test" list. What varies is the **acceptable
result**: `PASSED`, `REVIEW`, `WARNING` and `SKIPPED` are all certifiable; `FAILED` and `INTERRUPTED`
are not. Tests marked below as "skips when…" are certifiable in their skipped state.

The suite result enum is `PASSED, FAILED, WARNING, REVIEW, SKIPPED, UNKNOWN`
(`testmodule/TestModule.java`), with `REVIEW` commented as "test requires manual review". `[SUITE]`

### 2.2 Basic OP — full module list, in plan order

38 module entries are declared. With `client_registration=static_client`, **3 are dropped as not
applicable**, leaving **35 tests to run**. `[SUITE]`

Group A — `response_type=code`, `client_auth_type=client_secret_basic`, `response_mode=default`:

| # | Test id | Notes |
| --- | --- | --- |
| 1 | `oidcc-server` | Core happy-flow test; also covers ID Token verification, state, and static/dynamic `client_secret_basic` auth |
| 2 | `oidcc-response-type-missing` | **Manual review** (screenshot) |
| 3 | `oidcc-idtoken-signature` | **Not applicable with `static_client`** — dropped from the plan |
| 4 | `oidcc-idtoken-unsigned` | **Not applicable with `static_client`** — dropped from the plan |
| 5 | `oidcc-userinfo-get` | |
| 6 | `oidcc-userinfo-post-header` | |
| 7 | `oidcc-userinfo-post-body` | |
| 8 | `oidcc-ensure-request-without-nonce-succeeds-for-code-flow` | |
| 9 | `oidcc-scope-profile` | Skips if `scopes_supported` lacks `profile` |
| 10 | `oidcc-scope-email` | Skips if `scopes_supported` lacks `email` |
| 11 | `oidcc-scope-address` | Skips if `scopes_supported` lacks `address` |
| 12 | `oidcc-scope-phone` | Skips if `scopes_supported` lacks `phone` |
| 13 | `oidcc-scope-all` | Skips unless `scopes_supported` has `profile`, `email`, `phone` **and** `address` |
| 14 | `oidcc-alternate-happy-flow` | Java-suite-only test, no python equivalent |
| 15 | `oidcc-display-page` | |
| 16 | `oidcc-display-popup` | |
| 17 | `oidcc-prompt-login` | **Manual review** (screenshot of second login) |
| 18 | `oidcc-prompt-none-not-logged-in` | Expects an error redirect; error must be one requiring a UI |
| 19 | `oidcc-prompt-none-logged-in` | Two authorizations, same `sub`/`auth_time` |
| 20 | `oidcc-max-age-1` | **Manual review** (screenshot of second login); also asserts `auth_time` |
| 21 | `oidcc-max-age-10000` | |
| 22 | `oidcc-ensure-request-with-unknown-parameter-succeeds` | |
| 23 | `oidcc-id-token-hint` | |
| 24 | `oidcc-login-hint` | |
| 25 | `oidcc-ui-locales` | |
| 26 | `oidcc-claims-locales` | |
| 27 | `oidcc-ensure-request-with-acr-values-succeeds` | `acr` mismatch is a WARNING, not a failure |
| 28 | `oidcc-codereuse` | Reusing a code without error → WARNING, not failure |
| 29 | `oidcc-codereuse-30seconds` | `invalid_grant` is **required**; access token still working → WARNING |
| 30 | `oidcc-ensure-registered-redirect-uri` | **Manual review** (screenshot of redirect-URI error page) |
| 31 | `oidcc-ensure-post-request-succeeds` | POST to authorization endpoint; no callback in 30 s → WARNING |

Group B — `client_auth_type=client_secret_post`:

| # | Test id | Notes |
| --- | --- | --- |
| 32 | `oidcc-server-client-secret-post` | Same happy flow using `client_secret_post`; uses the `client_secret_post.*` config entry |

Group C — back to the Group A variants:

| # | Test id | Notes |
| --- | --- | --- |
| 33 | `oidcc-request-uri-unsigned-supported-correctly-or-rejected-as-unsupported` | **Not applicable with `static_client`** — dropped from the plan |
| 34 | `oidcc-unsigned-request-object-supported-correctly-or-rejected-as-unsupported` | Skips only if `request_object_signing_alg_values_supported` is present and lacks `none` |
| 35 | `oidcc-claims-essential` | Missing `name` claim → WARNING |
| 36 | `oidcc-ensure-request-object-with-redirect-uri` | **Manual review** (screenshot) when the OP rejects; same `none` skip rule as #34 |
| 37 | `oidcc-refresh-token` | Skips if the token endpoint returns no refresh token; uses `client2` |
| 38 | `oidcc-ensure-request-with-valid-pkce-succeeds` | A valid PKCE request must succeed whether or not the OP implements PKCE |

### 2.3 Config OP — full module list

**One** test. `[SUITE]`

| # | Test id | Notes |
| --- | --- | --- |
| 1 | `oidcc-discovery-endpoint-verification` | Fully automatic. No browser interaction, no screenshot. |

The plan class comments record which rows of the profiles table are folded into that single module:
issuer checks, endpoint checks, `jwks_uri`, JWKs validation, `scopes_supported`, `response_types_supported`,
`subject_types_supported`, `id_token_signing_alg_values_supported`, `claims_parameter_supported`,
and HTTPS-on-all-endpoints. The plan explicitly *drops* `OP-IDToken-none` from the PDF's Config row,
calling it "a mistake in the above PDF". `[SUITE]`

---

## 3. Which tests pause for manual human review

This is the part that drives the downstream negative-case evidence procedure, so here is the exact
mechanism rather than a guess.

### 3.1 Mechanism

1. A test calls an `Expect…` condition, which calls
   `AbstractCondition.createBrowserInteractionPlaceholder(msg)`. That logs an entry with
   `"upload": <placeholder id>` and `"result": REVIEW`.
2. The test then calls `performRedirectWithPlaceholder()` /
   `performRedirectAndWaitForPlaceholdersOrCallback()`, sets `Status.WAITING`, and calls
   `waitForPlaceholders()` — **the test blocks here**.
3. Either the human uploads a screenshot (`ImageAPI` then calls `test.fireTestReviewNeeded()`), or
   the suite's browser automation fills the placeholder, in which case `AbstractTestModule` sees
   `getFilledPlaceholders(...)` non-empty and calls `fireTestReviewNeeded()` itself.
4. `fireTestReviewNeeded()` sets the result to `REVIEW` unless the test already `FAILED`.

Net effect: **these tests never end in `PASSED`.** `REVIEW` is their best possible outcome, and
`REVIEW` is certifiable. `[SUITE]`, `[OP-TEST]`

### 3.2 The exact list — Basic OP

Five modules in the Basic plan create a browser-interaction placeholder. Verified by grepping every
`Expect*` / `createBrowserInteractionPlaceholder` usage across
`src/main/java/net/openid/conformance/openid/` and intersecting with the plan's module list. `[SUITE]`

| Test id | Condition | What the screenshot must show |
| --- | --- | --- |
| `oidcc-response-type-missing` | `ExpectResponseTypeMissingErrorPage` | "Upload a screenshot of the error page showing a missing response type error." |
| `oidcc-ensure-registered-redirect-uri` | `ExpectRedirectUriErrorPage` | "Show redirect URI error page" — the OP's own error page saying the redirect URI is invalid |
| `oidcc-ensure-request-object-with-redirect-uri` | `ExpectRedirectUriErrorPage` | Same, **only on the reject path**. If the OP honours the request object's `redirect_uri` (OIDCC-6.1) the flow completes via the callback and no screenshot is needed |
| `oidcc-prompt-login` | `ExpectSecondLoginPage` | "The server must ask the user to login for a second time; a screenshot of this must be uploaded." |
| `oidcc-max-age-1` | `ExpectSecondLoginPage` | Same message; the second login triggered by `max_age=1` |

Two of these — `oidcc-ensure-registered-redirect-uri` and `oidcc-response-type-missing` — are hard
negative cases: the OP **must not** redirect back to the client, so the only evidence available is a
screenshot of the OP-hosted error page. `oidcc-ensure-registered-redirect-uri` additionally throws
`TestFailureException` if the OP redirects to *either* the registered redirect URI or the bad one.

`oidcc-ensure-request-object-with-redirect-uri` is the one conditional case: whether it pauses
depends on which of the two permitted behaviours the OP picks.

### 3.3 Config OP

**None.** `oidcc-discovery-endpoint-verification` extends `AbstractTestModule` directly, runs
`configure()` then `start()` then `fireTestFinished()`, and creates no placeholder. `[SUITE]`

### 3.4 Not the same thing: browser interaction without review

Almost every Basic-OP test drives a browser to the authorization endpoint and needs a login to
happen. That is *interaction*, not *review* — it does not produce a `REVIEW` result. Several tests
also carry instructions in the blue box telling the tester to clear cookies first
(`oidcc-display-page`, `oidcc-display-popup`, `oidcc-prompt-none-not-logged-in`, `oidcc-login-hint`,
`oidcc-ui-locales`) or to visually confirm rendering (`display=popup` should produce a popup window;
`ui_locales` should render the login page in the requested locale). Those instructions are human
obligations but produce no automatic pause. `[SUITE]`, `[OP-TEST]`

---

## 4. What the plans demand of the discovery document and JWKS

All of this is `oidcc-discovery-endpoint-verification` unless noted. Severities are taken verbatim
from the module's `Condition.ConditionResult` arguments. `[SUITE]`

### 4.1 Transport and framing

| Check | Severity | Requirement |
| --- | --- | --- |
| `EnsureDiscoveryEndpointResponseStatusCodeIs200` | FAILURE | Discovery endpoint returns HTTP 200 |
| `CheckDiscoveryEndpointReturnedJsonContentType` | FAILURE | JSON content type |
| `CheckDiscEndpointAllEndpointsAreHttps` | FAILURE | **Every** metadata key ending in `_endpoint` must be a valid `https` URL — including non-OIDC extension endpoints |

### 4.2 Required fields and required values

| Field | Severity if wrong | Requirement |
| --- | --- | --- |
| `issuer` | FAILURE | Must be present, a valid URL, and must equal the discovery URL with `.well-known/openid-configuration` stripped (trailing slash ignored). The condition's error text: *"issuer listed in the discovery document is not consistent with the location the discovery document was retrieved from. These must match to prevent impersonation attacks."* |
| `response_types_supported` | FAILURE | With `static_client`: at least one of `code`, `code id_token`, `id_token`, `token id_token`, `code id_token token`, `code token`. (With `dynamic_client` it would require **all** of `code`, `id_token`, `token id_token` — not our case.) |
| `subject_types_supported` | FAILURE | Must contain at least one of `public` or `pairwise` |
| `id_token_signing_alg_values_supported` | FAILURE | **Must contain `RS256`** — "RS256 support is required" |
| `authorization_endpoint` | FAILURE | Present, valid, https |
| `token_endpoint` | FAILURE | Present, valid, https |
| `jwks_uri` | FAILURE | Present, valid, https |
| `grant_types_supported` | FAILURE | If present, must be a **non-empty array**. May be omitted (defaults to `authorization_code` + `implicit`) |
| `scopes_supported` | FAILURE if present and lacks `openid`; WARNING if absent | Should contain `openid` |
| `scopes_supported` syntax | FAILURE | Every entry must be a valid RFC 6749 §A.4 scope-token (visible ASCII, no SP / `"` / `\`) |
| `code_challenge_methods_supported` | FAILURE | May be omitted or an empty array. If a non-empty array, must contain `S256` or `plain`. **OIDC does not require PKCE support to be advertised.** |
| `ui_locales_supported` / other `*_locales*` | FAILURE on syntax, WARNING on non-canonical casing | Valid BCP-47-ish language tags |

### 4.3 Recommended but not required

| Field | Severity if missing | Note |
| --- | --- | --- |
| `userinfo_endpoint` | WARNING | "userinfo endpoint is recommended in the spec". If present it must be valid https (FAILURE otherwise) |
| `claims_supported` | WARNING | Must be an array if present; no required values |
| `registration_endpoint` | INFO | Skipped if absent. If present must be valid https |
| `userinfo_signing_alg_values_supported` | INFO | Skipped if absent |
| `request_object_signing_alg_values_supported` | INFO / WARNING | INFO if absent; WARNING if present and missing `RS256` |
| `request_parameter_supported`, `request_uri_parameter_supported`, `claims_parameter_supported` | INFO | Informational only |

### 4.4 What must NOT be advertised

There is **no hard "must not advertise" rule** in either plan. The nearest thing is:

- `CheckForUnexpectedParametersInServerMetadata` — **WARNING** (never a failure). It validates the
  metadata against the suite's superset schema
  (`src/main/resources/json-schemas/rfc8414/oauth_authorization_server_metadata.json`, 105 known
  properties, `additionalProperties: false`) and warns about any property not in that list. There is
  a documented escape hatch: adding a `server.allow_unexpected_metadata_fields` array to the test
  configuration suppresses the warning for legitimate extension metadata. `[SUITE]`
- `ValidateServerMetadataAgainstSchema` — **FAILURE**, but only for *structural* errors (wrong types
  or formats of fields that are present). Unknown-property errors are explicitly stripped out
  (`validationResult.withoutUnknownPropertyErrors()`), and the schema deliberately marks **no field
  as required**, including `issuer`. Presence requirements live in the individual `CheckDiscEndpoint*`
  conditions listed above. `[SUITE]`

The one genuine "must not" is indirect and applies to the whole plan, not the discovery document:
if you advertise `token_endpoint_auth_methods_supported` at all, it **must** contain
`client_secret_post`, or `oidcc-server-client-secret-post` fails
(`EnsureServerConfigurationSupportsClientSecretPost`, FAILURE, no fallback). By contrast,
`EnsureServerConfigurationSupportsClientSecretBasic` logs success when the field is *absent*
("so by default client_secret_basic support is supported") but fails if the field is present and
omits `client_secret_basic`. `[SUITE]`

### 4.5 JWKS

`CheckJwksUri` → `FetchServerKeys` → `ValidateJwksSequence("server_jwks", null, "server JWKS", "OIDCD-3")`:
`[SUITE]`

| Check | Severity | Requirement |
| --- | --- | --- |
| `EnsureJwksHasNoPrivateOrSymmetricKeyMaterial` | **FAILURE** | The published JWKS must contain **public keys only** — no private components, no symmetric keys |
| `ValidateJwksStructure` | FAILURE | Valid JWK Set structure, required fields present, unpadded base64url encoding |
| `ParseUsableJwksKeys` | FAILURE | Keys must actually parse |
| `WarnOnUnusableJwksKeys` | WARNING | Unknown `kty` / curve / `alg` |

The suite's separate `ValidateServerJWKs` condition summarises the bar as: "keys are valid JSON,
contain the required fields and are correctly encoded using unpadded base64url".

---

## 5. Client configuration the suite expects — and the `certification` seed

### 5.1 What the suite expects

`[OP-TEST]` states that for Basic/Implicit/Hybrid, an OP that is not using Dynamic Client
Registration must manually register **three** clients:

1. a client supporting `client_secret_basic`;
2. a second client supporting `client_secret_basic`, "required for authorization code binding tests";
3. a client supporting `client_secret_post` — "may overlap with the first client".

The suite source confirms all three slots exist as distinct configuration keys when
`client_registration=static_client`: `[SUITE]`

| Suite config key | Consumed by | Purpose |
| --- | --- | --- |
| `client.client_id` / `client.client_secret` | every module except the post test | primary `client_secret_basic` client |
| `client2.client_id` / `client2.client_secret` | `AbstractOIDCCMultipleClient` → **only `oidcc-refresh-token`** in the Basic plan | second client; the test issues a refresh token to client 2 and then tries to redeem it as client 1, which must fail |
| `client_secret_post.client_id` / `client_secret_post.client_secret` | `OIDCCServerTestClientSecretPost` | `client_secret_post` client. The class comment: "as the basic etc certification profiles run tests with different client authentication types, we need to allow the user to provide multiple clients if using static clients (as many/most servers restrict each client to using only one authentication method)" |

**Redirect URI.** All clients must have `https://www.certification.openid.net/test/a/<ALIAS>/callback`
registered, where `<ALIAS>` is the tester-chosen alias. `[OP-TEST]`

**PKCE.** The plan never requires PKCE support. `oidcc-ensure-request-with-valid-pkce-succeeds`
requires only that a *valid* PKCE request **succeed** — its rationale is RFC 6749 §3.1's
"the authorization server MUST ignore unrecognized request parameters". Meanwhile 34 of the 35
Basic-OP tests send **no** `code_challenge` at all. So **PKCE must not be mandatory** for the
certification clients. `[SUITE]`

### 5.2 Cross-check against `src/OpenIdentityStack.DbMigrator/Program.cs`

The `certification` seed profile (`SeedCertificationDataAsync`, lines ~256–290) creates exactly three
clients with a shared redirect-URI list:

```
oidf-code-client            -> client.client_id
oidf-code-client-post       -> client_secret_post.client_id
oidf-code-client-takeover   -> client2.client_id
```

Redirect URIs come from `GetCertificationRedirectUris` (lines 498–518):
`https://www.certification.openid.net/test/a/{alias}/callback`, plus the staging equivalent when
`Seed:Certification:IncludeStagingRedirectUri` is true (default).

**Verdict: the three-client seed is correct in shape, count and redirect URIs.** It maps 1:1 onto the
three configuration slots the suite exposes, and the naming is apt — "takeover" is exactly what
`oidcc-refresh-token`'s `performSecondClientTests()` does ("Attempting to use refresh_token issued to
client 2 with client 1").

Points to verify or change before the run:

1. **PKCE must stay optional per client.** `OpenIddictSetup.cs` calls `options.AllowAuthorizationCodeFlow()`
   with the comment that PKCE is enforced per client "so confidential certification clients can still
   exercise the non-PKCE Basic OP conformance path". `SeedCertificationClientAsync` sets no PKCE
   requirement, which is correct. Do not add `Requirements.Features.ProofKeyForCodeExchange` to these
   three clients.
2. **`address` and `phone` scopes are not seeded and not registered.** The seed grants
   `openid profile email offline_access` only, and `OpenIddictSetup.cs` calls
   `options.RegisterScopes(profile, email, roles, "api")`. Consequence:
   `oidcc-scope-address`, `oidcc-scope-phone` and `oidcc-scope-all` will end **SKIPPED**, which is
   certifiable — but it is a deliberate scope reduction, not a pass. OpenIddict's own certification
   sample registers `address`, `email`, `phone`, `profile` and grants `scp:address`/`scp:phone` to
   both clients. `[OIDDICT-SAMPLES]` **Decision needed**, not a defect.
3. **Client-authentication method is not pinned per client.** The seed marks all three
   `ClientType = Confidential` and does not restrict the token-endpoint auth method, so all three
   accept both `client_secret_basic` and `client_secret_post`. That satisfies the suite (the post
   client only has to accept post) and matches `[OP-TEST]`'s "may overlap with the first client".
   No change needed. *Not independently verified against OpenIddict's source in this research* —
   see §6.
4. **`oidf-code-client-takeover` needs the same grants as the primary client.** It does — all three
   get `GrantTypes.AuthorizationCode`, `GrantTypes.RefreshToken`, `ResponseTypes.Code` and the
   `offline_access` scope permission, which `oidcc-refresh-token` needs.
5. **`ConsentType = ConsentTypes.Implicit`.** `oidcc-refresh-token` adds `prompt=consent` to the
   authorization request when the scope contains `offline_access`
   (`AddPromptConsentToAuthorizationEndpointRequestIfScopeContainsOfflineAccess`). With implicit
   consent there is no consent screen to show. The suite does **not** assert that a consent screen
   appears, so this should be benign — but it is worth watching in the first run. *Not settled by
   any source read here.*

---

## 6. OpenIddict-specific conformance gotchas

Primary/high-trust findings only. There is no OpenIddict "conformance gotchas" document; what
follows is what the primary sources actually say.

### 6.1 OpenIddict's own position on certification

From `openiddict/openiddict-samples` `README.md`: `[OIDDICT-SAMPLES]`

> OpenIddict is not a turnkey solution but a framework that requires writing custom code to be
> operational (typically, at least an authorization controller), making it a poor candidate for the
> certification program.

and

> developers are encouraged to execute the conformance tests against their own deployment

So there is no upstream certification to inherit — every deployment must be tested on its own. There
*is* precedent: the OIDF certified-implementations list contains **ReadyMembers v6.0** (C# ASP.NET
Core, OpenIddict 3.1) certified for **Basic OP, Implicit OP, Hybrid OP, Config OP and FormPost OP**.
`[CERT-LIST]` That establishes Basic OP + Config OP are achievable on an OpenIddict base.

### 6.2 The `Contruum` reference sample

`samples/Contruum/Contruum.Server` is OpenIddict's purpose-built certification-suite sample.
`[OIDDICT-SAMPLES]` Concrete things it does that our setup does not:

| Contruum | OpenIdentityStack |
| --- | --- |
| `RegisterScopes("address", "email", "phone", "profile")` | `RegisterScopes(profile, email, roles, "api")` — no `address`, no `phone` |
| `RegisterClaims(...)` includes `address`, `phone_number`, `phone_number_verified` | those three are absent from `OpenIddictSetup.cs` |
| Two clients (`oidc_certification_app_1`, `oidc_certification_app_2`), both `confidential`, both with `scp:address`, `scp:email`, `scp:phone`, `scp:profile` | three clients, scopes `openid profile email offline_access` |
| Handles `HandleUserInfoRequestContext` with an **inline event handler** that populates the standard claims from the access token principal; token endpoint pass-through is **deliberately not enabled** ("so that token requests are automatically handled by OpenIddict") | uses `EnableUserInfoEndpointPassthrough()` and `EnableTokenEndpointPassthrough()` |
| `AddEphemeralSigningKey()` / `AddEphemeralEncryptionKey()`, with the note that this makes the key-rotation test easy to run by restarting | certificate-based signing/encryption; ephemeral only in Development/Testing |

The `EnableTokenEndpointPassthrough()` difference is the one worth a second look: Contruum
deliberately leaves the token endpoint to OpenIddict. Our repo takes it over. Nothing in the suite
sources says pass-through is disallowed — this is a *risk flag*, not a documented failure.

### 6.3 Request objects — the sharpest edge

`oidcc-unsigned-request-object-supported-correctly-or-rejected-as-unsupported` and
`oidcc-ensure-request-object-with-redirect-uri` skip **only** when
`request_object_signing_alg_values_supported` is *present in discovery and does not contain `none`*
(`AbstractOIDCCServerTest.skipTestIfNoneUnsupported`). If the field is **absent altogether, the tests
run.** `[SUITE]`

When they run, the OP must do one of exactly two things:

- process the unsigned request object correctly (including letting the request object's
  `redirect_uri` win, per OIDCC-6.1); or
- return `request_not_supported`.

The suite's own summary is explicit that the middle path is a failure: *"the python suite allowed
implementations to completely ignore the request object — this was not compliant with the spec, and
in this test either the object must be processed or `request_not_supported` must be returned"*.
`[SUITE]`

The repo already carries a handler for this (`options.AddAuthorizationErrorRedirects()` in
`OpenIddictSetup.cs`, commented "Redirect request-object capability errors back to validated
clients", implemented in `AuthorizationErrorRedirectHandlers.cs`), which suggests this edge was hit
before. **Verify what our discovery document actually publishes for
`request_object_signing_alg_values_supported` before the run** — it determines whether two tests run
or skip, and one of them is a manual-review test.

### 6.4 Metadata surface

`OpenIddictSetup.cs` enables introspection, revocation, end-session, and session management
(`options.AddSessionManagement()` → `check_session_iframe`). All of the corresponding metadata names
(`introspection_endpoint`, `revocation_endpoint`, `end_session_endpoint`, `check_session_iframe`) are
in the suite's known-property schema, so they will not trigger the unexpected-parameter warning.
But `CheckDiscEndpointAllEndpointsAreHttps` will validate **every** one of them as an https URL.
`[SUITE]` A misconfigured `OpenIddict:Issuer` produces http URLs and fails Config OP outright.

### 6.5 Not established by any source read here

- Whether OpenIddict includes `offline_access` in `scopes_supported` automatically when the refresh
  token flow is enabled. Not verified; check the live discovery document.
- Whether OpenIddict's access-token encryption (enabled outside Development/Testing in this repo)
  interacts badly with any suite check. Nothing in the suite validates the access token's internal
  format — it is treated as opaque and only used as a bearer credential at the userinfo endpoint —
  so there is no reason to expect a problem, but no source confirms it either.
- Whether `ConsentType.Implicit` plus `prompt=consent` (§5.2 item 5) causes any suite assertion to
  trip.

---

## 7. Definition of "green"

1. `oidcc-config-certification-test-plan` — 1 test, fully automatic, must end `PASSED` or `WARNING`.
2. `oidcc-basic-certification-test-plan` with `server_metadata=discovery` and
   `client_registration=static_client` — **35 tests run** (3 of the 38 declared modules are dropped
   as not applicable to static clients).
3. Every test ends in `PASSED`, `REVIEW`, `WARNING` or `SKIPPED`. No `FAILED`, no `INTERRUPTED`.
   `[OP-TEST]`
4. Exactly **5** Basic-OP tests will pause and require a human-uploaded screenshot; 4 of them
   unconditionally, 1 (`oidcc-ensure-request-object-with-redirect-uri`) only on the reject path.
   Those tests can never read better than `REVIEW`.
5. Expect legitimate `SKIPPED` results for `oidcc-scope-address`, `oidcc-scope-phone` and
   `oidcc-scope-all` unless `address`/`phone` are added to `scopes_supported`.
