# Production administrator MFA

Every human using Management Web or another delegated Admin API client must authenticate with an approved second factor. The authority checks the evidence when issuing administrative credentials and the API checks it when those credentials are presented. Existing sessions without that evidence require a new sign-in. The client's administrative entitlement, its permission ceiling, the user's current permissions, and local disablement all still apply. Machine clients continue to use their separately approved application permissions.

Production, staging, and custom environments enforce this policy. Development and Testing can exercise the same checks with `AdministrativeMfa__Enforce=true`. With no approved sign-in method, human administrative access is denied. Roll out the trust configuration to every API replica together.

## Routine operators

The identity administrator must configure the upstream OIDC client to require phishing-resistant MFA, such as a hardware-backed passkey, and to return a distinct authentication context (`acr`) for that assurance level in the validated ID token. Verify with the provider owner which authentication and recovery methods qualify for that context. A provider that issues the same context for password-only sessions cannot serve as an administrator MFA provider.

The deployment owner configures one exact context per trusted provider registration. Substitute the registration's immutable GUID, the precise issuer from discovery (including any trailing slash), and the provider's assurance value. The values below illustrate the configuration shape.

```text
AdministrativeMfa__TrustedProviders__<provider-guid>__Issuer=https://login.example/tenant
AdministrativeMfa__TrustedProviders__<provider-guid>__Acr=urn:example:staff:hardware-mfa
```

The authentication handler binds the evidence to the validated ID token, provider, and federated subject. A password-only local fallback session, a group claim, an MFA flag on the user record, or a requested `acr_values` parameter supplies no assurance. Changing the trusted tuple retires its previous evidence once the new configuration is active across the deployment. Restart the API replicas to apply changes. Revoke affected sessions when withdrawing trust.

If Management Web reports an authorization denial after sign-in, an operator can look for `Authentication.AdministrativeMfaDenied` in the audit. The entry identifies the local account and the affected authorization, code-exchange, or refresh flow. Verify the upstream policy and start a fresh sign-in with the approved provider.

Sensitive approvals still require actual human authentication within five minutes. A federated administrator needs a trustworthy upstream `auth_time` for those operations. An MFA session can remain sufficient for ordinary administration after that window. Cookie renewal and refresh-token exchange preserve the original authentication event. [Unrestricted administration](unrestricted-administration.md)

## Bootstrap and emergency access

The deployment owner and the security officer maintain one independently accessible local Emergency Administrator. Keep its unique password and client-certificate private key in separately controlled custody. Prefer a hardware-backed, non-exportable key. The emergency credential is used at sign-in. Subsequent browser and API requests use normal session and bearer credentials.

1. Use the documented migrator bootstrap to create the first active administrator with the explicit All-Permissions Grant and prepare the Management Web Client's entitlement. Record its immutable local user ID from the bootstrap audit. Existing installations should designate a reviewed active local account with its own password and that explicit grant. [Bootstrap and upgrades](../operations/upgrades.md) · [Administrative client access](administrative-client-access.md)
2. Issue a client-authentication certificate and record its full SHA-256 certificate fingerprint (64 hexadecimal digits). Configure `AdministrativeMfa__Emergency__UserId` and `AdministrativeMfa__Emergency__CertificateSha256` on the API. Keep the private key with its custodian. When federation is the default, enable local fallback before the outage drill.
3. Prepare a dedicated operator-only HTTPS listener on the **same issuer hostname and a separate port** for local sign-in. Use direct Kestrel TLS or TLS passthrough. Configure `Kestrel:Endpoints:<name>:ClientCertificateMode=AllowCertificate`, the server certificate, and trust for the client certificate chain. Restrict the listener at the network boundary. The browser presents its own certificate in the TLS handshake. A forwarded certificate header cannot supply this proof. Keep the normal public issuer listener configured for conventional browser clients so the browser can exchange its code without presenting a TLS client certificate at the token endpoint. Both listeners must share the cookie and Data Protection configuration. [Kestrel HTTPS endpoint configuration](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/endpoints#configure-client-certificates-in-appsettingsjson)
4. Open `/Account/Login` on the recovery port, choose local fallback, present the certificate, and sign in with the emergency account password. In the same browser profile, open Management Web at its usual address and sign in using the normal issuer. Its authorization request uses the cookie shared across ports on the same hostname and its token exchange uses the registered public endpoint. Repeat the local sign-in before a fresh approval when needed. Establish routine federated administrative access in a separate browser profile before making federation the default. Preserve independent emergency access when changing providers.

### Outage drill

Use a test deployment with the upstream unavailable and a fresh browser profile. Record the release revision, time, operator, and pass/fail outcomes. Keep passwords, tokens, cookies and private keys out of evidence.

| Exercise | Expected outcome |
| --- | --- |
| Use local fallback with the designated account and its certificate | Complete code exchange with PKCE, revoke a disposable test session through the Admin API, and confirm its persisted status is revoked. A protected approval still requires fresh authentication and acknowledgement. |
| Use the designated account with its correct password and no certificate | Emergency login is denied. No administrator session is issued. |
| Present the certificate with a different local account | Emergency login is denied, including for another account that holds the All-Permissions Grant. |
| Inspect the audit as an authorized reviewer | `EmergencyAdministrator.Authenticated` or `EmergencyAdministrator.Denied` identifies the authenticated local actor and outcome of the emergency attempt. Invalid passwords follow the normal credential policy. |

After use, review the audit and administrative mutations, revoke the emergency session, and return both factors to their custodians. For suspected compromise, revoke sessions, disable the account if appropriate, retire the old certificate pin on every replica, rotate the affected credentials, and repeat the drill. The security officer owns recovery approval. The deployment owner distributes trust changes. Maintaining a working recovery method is a prerequisite for rollout.

## Verification evidence

The automated federation exercise uses signed ID tokens from a test provider and the registered public Management Web Client. An approved context completes PKCE and returns the operator's permitted actions from `/api/me`. A lower context returns `access_denied` without an authorization code. The freshness regression tests exercise the approval boundary at 300 and 301 seconds after the original event, including after refresh. [Federated sign-in tests](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/tests/OpenIdentityStack.Api.Tests/Authentication/FederatedAdministrativeSignInTests.cs) · [Authorization tests](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/tests/OpenIdentityStack.Api.Tests/Authentication/AuthorizationControllerTests.cs)

The emergency exercise uses two real loopback HTTPS listeners, a disposable client certificate, a local account database and an unavailable upstream. The designated account revokes a disposable session. The test checks the persisted revocation and audit outcomes and covers missing credentials, a different account and a withdrawn grant. Release operators should record a separate rehearsal of their actual certificate custody, upstream policy, TLS routing and Management Web deployment. [Emergency recovery test](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/tests/OpenIdentityStack.Api.Tests/Authentication/EmergencyAdministratorTests.cs)
