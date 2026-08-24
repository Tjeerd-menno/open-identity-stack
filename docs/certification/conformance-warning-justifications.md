# Conformance Warning Justifications

The OIDF conformance suite grades each test module `PASSED`, `WARNING`, `REVIEW`,
`SKIPPED` or `FAILED`. Only `FAILED` blocks certification, but
[`run-oidf-conformance-suite.md`](run-oidf-conformance-suite.md) requires every
non-`PASSED` result to be reviewed and justified before the results are
submitted. This document is that record.

## Basic OP: `oidcc-scope-email` and `oidcc-alternate-happy-flow` — WARNING

### What the suite reports

Both modules warn for one reason: the ID token carries the claims associated
with the requested `profile` and `email` scopes, even though an access token was
also issued. The suite condition is
`EnsureIdTokenDoesNotContainEmailForScopeEmail`, citing OIDCC-5.4.

The relevant sentence of OpenID Connect Core §5.4 is:

> The Claims requested by the `profile`, `email`, `address`, and `phone` scope
> values are returned from the UserInfo Endpoint […] when a `response_type`
> value is used that results in an Access Token being issued.

The suite's own result text concedes that returning these claims in the ID token
"does not violate the specification", which is why the modules warn rather than
fail.

### Decision

**Accepted as designed. No change to claim emission.**

OpenIdentityStack returns the `profile`, `email`, `address` and `phone` scope
claims in **both** the ID token and the UserInfo response. UserInfo is fully
implemented and returns the same values, so a relying party that follows §5.4
literally — request the scope, then call UserInfo — gets exactly what the
specification describes. The ID token simply also carries them.

### Rationale

**The behaviour is spec-compliant, and the suite says so.** §5.4 describes where
scope-requested claims are returned from; it does not forbid the OP from also
placing them in the ID token, and the suite grades accordingly. Nothing in the
Basic OP profile is violated.

**Removing them would break the most common relying-party integration.**
Reading `email`, `name` or `preferred_username` directly from the validated ID
token, without a UserInfo round-trip, is the default behaviour of widely used
OIDC client libraries — including the ASP.NET Core OpenID Connect handler, which
maps ID token claims into the authentication cookie out of the box. An OP that
omits these claims from the ID token forces every such relying party to add an
explicit UserInfo call. That is a real cost imposed on integrators to satisfy a
sentence the conformance suite already accepts as satisfied.

**The escape hatch in §5.4 is unreachable in this OP.** The sentence is
conditional on an access token being issued; when none is (the `id_token`
`response_type`), the claims belong in the ID token. OpenIdentityStack enables
only the authorization code, client credentials and refresh token flows —
implicit and hybrid are deferred, see
[`openid-connect-certification-scope.md`](openid-connect-certification-scope.md)
— so an access token is *always* issued. Honouring §5.4's preference would
therefore mean unconditionally never emitting these claims in the ID token, not
adding a conditional branch. It is an all-or-nothing behavioural change, not a
refinement.

### Consequences accepted

**The ID token carries personal data.** It is the artifact relying parties are
most likely to log, cache and pass between services, so `email`, `name`,
`address` and `phone_number` travel further than they would if UserInfo were the
only source. This is a genuine privacy cost and it is accepted knowingly: in the
authorization code flow the ID token is delivered over the back channel and is
never exposed in a redirect URI or browser history, and relying parties that
prefer the minimal surface can simply request fewer scopes.

**The rule applies uniformly to all four §5.4 scopes.** `address` and `phone`
follow the same emission rule as `profile` and `email`. Treating the newer
scopes differently would encode a distinction in the destination logic that no
reader could derive from the code, and would leave this document explaining why
the OP handles its four §5.4 scopes two different ways.

**This is not an endorsement of ID token bloat generally.** Claims are emitted
into the ID token only when the corresponding scope was granted; nothing is
included that the relying party did not ask for. The suite's
`EnsureIdTokenDoesNotContainNonRequestedClaims` check confirms this and passes.

### Revisiting

If an implicit or hybrid flow profile is ever added, §5.4's conditional becomes
reachable and the destination logic in
[`TokenClaimProjectionService`](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/src/OpenIdentityStack.Api/Authentication/TokenClaimProjectionService.cs)
must gain the "is an access token being issued" branch it does not need today.

Decided on
[#322](https://github.com/Tjeerd-menno/open-identity-stack/issues/322).
