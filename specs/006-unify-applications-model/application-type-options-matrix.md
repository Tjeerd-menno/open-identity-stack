# Application Profile Options Matrix

**Feature**: Replace separate Clients and Service Accounts with a unified `Application` aggregate  
**Purpose**: Define which configuration options should be visible, fixed, advanced, or unavailable for each application profile.

## Legend

| Symbol | Meaning |
|---|---|
| ✅ | Available in the normal UI/API |
| 🔒 | Fixed default; visible as read-only or implicit |
| ⚙️ | Advanced option; available only when the deployment supports it |
| ❌ | Not available; hide from UI/API for this application profile |

## Definitions

- **Web**: server-hosted web application where the OAuth client executes on a backend that can keep credentials confidential.
- **Single Page**: browser-based JavaScript application where the OAuth client executes in the browser.
- **Native**: installed desktop or mobile application.
- **Machine to Machine**: non-interactive workload acting on its own behalf.
- **Device**: input-constrained device using the OAuth Device Authorization Grant.

A browser app with a Backend-for-Frontend (BFF) should be registered as **Web**, not **Single Page**, because the backend is the OAuth client and can keep tokens and credentials out of the browser.

## Recommended Default Profiles

| Application profile | Default client profile | Default grant types | Redirect configuration | Credential options | PKCE | Refresh tokens | Reasoning |
|---|---|---|---|---|---|---|---|
| Web | Confidential | `authorization_code`; optionally `refresh_token` | Required HTTPS redirect URIs; post-logout redirect URIs optional | Client secret by default; `private_key_jwt` and mTLS optional advanced methods | Default on / recommended | Optional | A server-hosted web app can protect client credentials. Authorization Code keeps tokens out of the front channel, and current OAuth security guidance recommends PKCE even for confidential clients. |
| Single Page | Public | `authorization_code`; optionally `refresh_token` | Required HTTPS redirect URIs and allowed web origins | No secrets, no certificates | Required | Optional only with rotation and bounded lifetime | Browser JavaScript cannot keep a shared secret. Current browser-app guidance is Authorization Code + PKCE and no Implicit flow. |
| Native | Public | `authorization_code`; optionally `refresh_token` | Required native redirect URI: app-claimed HTTPS preferred; private scheme and loopback supported | No shared secret | Required | Optional | Native apps are public clients; static secrets can be extracted. Use an external user-agent and PKCE. |
| Machine to Machine | Confidential | `client_credentials` only | None | Client secret, `private_key_jwt`, and/or mTLS; multiple credentials with rotation | Not applicable | Not available | Client Credentials is for confidential clients acting on their own behalf and has no end-user redirect step. |
| Device | Public by default; confidential only with extra protection | `urn:ietf:params:oauth:grant-type:device_code`; optionally `refresh_token` | None | No shared secret by default; advanced key/certificate option only for managed trusted devices | Not applicable | Optional | Device flow is for devices without a usable browser/input path; users approve on a secondary device while the client polls the token endpoint. Device clients are generally public unless they can protect credentials. |

## Detailed Option Availability Matrix

| Option | Web | Single Page | Native | Machine to Machine | Device | Reasoning / rule |
|---|---:|---:|---:|---:|---:|---|
| Application name, description, owner, tags | ✅ | ✅ | ✅ | ✅ | ✅ | Metadata is independent of OAuth flow and should be available for administration, consent screens, and auditability. |
| Enable / disable application | ✅ | ✅ | ✅ | ✅ | ✅ | Lifecycle control should be universal. Disabling an application should prevent new token issuance for every application profile. |
| `client_id` | ✅ | ✅ | ✅ | ✅ | ✅ | Every OAuth client registration needs a stable public identifier. It is not a secret and must not be used alone for authentication. |
| Application profile | 🔒 | 🔒 | 🔒 | 🔒 | 🔒 | The selected type determines the allowed configuration surface. Changing type after creation should be blocked or implemented as a migration workflow, because it changes security posture. |
| Client profile | 🔒 Confidential | 🔒 Public | 🔒 Public | 🔒 Confidential | 🔒 Public by default / ⚙️ Confidential | OAuth distinguishes clients by their ability to keep credentials confidential. Web and M2M can usually protect credentials; SPA and native clients cannot. Device clients are generally public unless additional protection is available. |
| Token endpoint authentication method | ✅ `client_secret_basic` default; ⚙️ `client_secret_post`, `private_key_jwt`, mTLS | 🔒 `none` | 🔒 `none` | ✅ `client_secret_basic` default; ⚙️ `client_secret_post`, `private_key_jwt`, mTLS | 🔒 `none`; ⚙️ confidential device methods | Public clients should not authenticate with shared secrets. Confidential clients need an explicit token endpoint authentication method. |
| Client secrets | ✅ | ❌ | ❌ | ✅ | ❌ by default / ⚙️ only for confidential device | Shared secrets are appropriate for confidential clients. They should be hidden for SPA/native clients because users can inspect or extract them. |
| Multiple active secrets | ✅ | ❌ | ❌ | ✅ | ⚙️ | Needed for safe rotation without downtime for confidential clients. |
| Secret rotation and revocation | ✅ | ❌ | ❌ | ✅ | ⚙️ | Rotation is a lifecycle feature of confidential credentials. It is not meaningful for public clients that do not have a secret. |
| Client certificates / mTLS | ⚙️ | ❌ | ❌ | ⚙️ | ⚙️ managed devices only | mTLS is useful for high-assurance confidential clients and certificate-bound tokens. It should not be presented as a normal option for public clients. |
| JWKS / JWKS URI for client authentication | ⚙️ | ❌ | ⚙️ only for proof-of-possession features, not shared client auth | ⚙️ | ⚙️ | Useful for `private_key_jwt`, signed requests, and some sender-constrained token models. For native/device clients, treat as advanced and profile-specific. |
| Allowed scopes | ✅ | ✅ | ✅ | ✅ | ✅ | Scope authorization applies to every token-issuing application profile. |
| Allowed audiences / resource servers | ✅ | ✅ | ✅ | ✅ | ✅ | This should be universal to constrain where issued access tokens can be used. |
| Authorization Code grant | ✅ | ✅ | ✅ | ❌ | ❌ | User-interactive browser/native login uses Authorization Code. M2M does not involve a user. Device flow uses a separate device authorization grant instead. |
| Device Code grant | ❌ | ❌ | ❌ | ❌ | ✅ | Device Code is specific to input-constrained devices where authorization happens on a secondary device. |
| Client Credentials grant | ❌ by default / ⚙️ only if explicitly allowing hybrid apps | ❌ | ❌ | ✅ | ❌ | Client Credentials is for confidential clients acting on their own behalf. Keep it exclusive to M2M unless intentionally supporting hybrid registrations. |
| Refresh Token grant | ✅ optional | ✅ optional with rotation/lifetime rules | ✅ optional | ❌ | ✅ optional | Refresh tokens are for continuing user-delegated access. M2M can request new access tokens using its client credentials and does not need refresh tokens. |
| Implicit grant | ❌ | ❌ | ❌ | ❌ | ❌ | Do not expose this for new applications. Browser-based clients should use Authorization Code + PKCE, and security guidance deprecates Implicit-style token delivery. |
| Resource Owner Password Credentials grant | ❌ | ❌ | ❌ | ❌ | ❌ | Do not expose this for new applications. It bypasses federated login and modern phishing-resistant authentication patterns. |
| Redirect URIs | ✅ required for Authorization Code | ✅ required | ✅ required | ❌ | ❌ | Redirect URIs are needed only for redirect-based flows. M2M and Device do not receive authorization responses through a redirect URI. |
| Redirect URI exact matching | ✅ | ✅ | ✅ | ❌ | ❌ | All redirect-based clients should use exact matching. Native loopback redirects are the special case where the port must be variable. |
| Native app redirect URI type | ❌ | ❌ | ✅ app-claimed HTTPS preferred; private scheme and loopback supported | ❌ | ❌ | Native apps need platform-specific redirect handling. Prefer claimed HTTPS when available; support custom schemes and loopback for desktop/mobile interoperability. |
| Loopback redirect URI with dynamic port | ❌ | ❌ | ✅ | ❌ | ❌ | This is a native desktop pattern; allow any port only for loopback IP redirects. |
| Post-logout redirect URIs | ✅ | ✅ | ✅ | ❌ | ❌ | Relevant to browser/user-session logout. Not useful for M2M and generally not useful for Device flow. |
| Allowed web origins / CORS origins | ⚙️ only for browser-calling APIs | ✅ required when browser exchanges codes/calls APIs | ❌ | ❌ | ❌ | SPA clients need explicit origins because browser JavaScript uses cross-origin requests. A classic Web/BFF client usually does not call the token endpoint from browser code. |
| PKCE required | ✅ default on / recommended | ✅ required | ✅ required | ❌ | ❌ | Public redirect-based clients must use PKCE. Confidential Web clients should also use it because it protects against code injection and CSRF-like misuse. |
| Consent requirement | ✅ | ✅ | ✅ | ❌ | ✅ | Consent is meaningful when a human user authorizes delegated access. M2M has no resource owner interaction during token issuance. |
| Login initiation URI | ⚙️ | ⚙️ | ❌ | ❌ | ❌ | Useful for web/browser login initiation patterns; not relevant to M2M and usually not relevant to installed/native clients. |
| Front-channel logout | ⚙️ | ⚙️ | ❌ | ❌ | ❌ | Browser session coordination only. |
| Back-channel logout | ⚙️ | ⚙️ if backend exists | ⚙️ only with backend endpoint | ❌ | ❌ | Requires an application-controlled backend endpoint. Pure SPA/native clients cannot reliably receive server-to-server logout notifications. |
| Device user-code settings | ❌ | ❌ | ❌ | ❌ | ✅ | Device flow needs user-code length, expiry, polling interval, and verification URI UX controls. |
| Device polling interval and expiry | ❌ | ❌ | ❌ | ❌ | ✅ | Device clients poll while waiting for user authorization; the server should control interval and expiry. |
| Token lifetime overrides | ⚙️ | ⚙️ | ⚙️ | ⚙️ | ⚙️ | Useful but should stay advanced; defaults should come from tenant/security policy. |
| Sender-constrained access tokens | ⚙️ mTLS / DPoP | ⚙️ DPoP only, with caveats | ⚙️ DPoP | ⚙️ mTLS / DPoP | ⚙️ DPoP/mTLS for managed devices | Advanced high-assurance feature. mTLS fits confidential clients; DPoP may fit public clients but is not a substitute for preventing malicious code in browser apps. |
| Public API read-only view of registration | ✅ | ✅ | ✅ | ✅ | ✅ | Safe administrative view should be universal, but never expose secret material after creation/rotation. |

## Recommended API Shape

### Application Profile Enum

```csharp
public enum ApplicationProfile
{
    Web = 1,
    SinglePage = 2,
    Native = 3,
    MachineToMachine = 4,
    Device = 5
}
```

### Option Availability Enum

```csharp
public enum ApplicationOptionAvailability
{
    Hidden = 0,
    ReadOnly = 1,
    Available = 2,
    Advanced = 3
}
```

### Policy Object

```csharp
public sealed record ApplicationProfilePolicy(
    ApplicationProfile ApplicationProfile,
    ClientProfile DefaultClientProfile,
    IReadOnlySet<string> AllowedGrantTypes,
    IReadOnlySet<string> DefaultGrantTypes,
    IReadOnlyDictionary<string, ApplicationOptionAvailability> Options);

public enum ClientProfile
{
    Public = 1,
    Confidential = 2
}
```

## Implementation Notes

1. Store `ApplicationProfile` as a domain-level classification, not as a direct OAuth protocol value.
2. Derive OpenIddict permissions from `AllowedGrantTypes`, `AllowedScopes`, redirect URIs, and token endpoint authentication method.
3. Keep `client_id` globally unique across all applications.
4. Do not permit hybrid defaults. A single application should not be both Web and M2M unless an explicit future feature introduces multi-profile applications.
5. Keep compatibility endpoints such as `/api/admin/clients` and `/api/admin/service-accounts` as thin adapters only if needed for migration. Internally they should call `Applications` use cases.
6. Never return existing secret values from read APIs. Return secret material only once on creation or rotation.

## References

- RFC 6749 — OAuth 2.0 Authorization Framework: https://www.rfc-editor.org/rfc/rfc6749.html
- RFC 7591 — OAuth 2.0 Dynamic Client Registration Protocol: https://www.rfc-editor.org/rfc/rfc7591.html
- RFC 8252 — OAuth 2.0 for Native Apps: https://www.rfc-editor.org/rfc/rfc8252.html
- RFC 8628 — OAuth 2.0 Device Authorization Grant: https://www.rfc-editor.org/rfc/rfc8628.html
- RFC 8705 — OAuth 2.0 Mutual-TLS Client Authentication and Certificate-Bound Access Tokens: https://www.rfc-editor.org/rfc/rfc8705.html
- RFC 9700 — Best Current Practice for OAuth 2.0 Security: https://www.rfc-editor.org/rfc/rfc9700.html
- OAuth 2.0 for Browser-Based Applications, IETF draft in RFC Editor queue as of 2026-05-24: https://datatracker.ietf.org/doc/draft-ietf-oauth-browser-based-apps/
