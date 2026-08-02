# Running the OpenID Foundation Conformance Suite

## Purpose

This document explains how to run the OpenID Foundation conformance tests against OpenIdentityStack.

## Test Target

OpenIdentityStack certification environment:

```text
https://<public-issuer-host>/
```

## Discovery URL

```text
https://<public-issuer-host>/.well-known/openid-configuration
```

## Initial Test Plan

Use the hosted OpenID Foundation conformance suite and select:

```text
OpenID Connect Core: Basic Certification Profile Authorization server test
```

Use a test plan in the "Test an OpenID Provider" section.

## Client Registration Mode

Use static client registration for the first certification milestone.

Seeded clients:

```text
oidf-code-client
oidf-code-client-post
oidf-code-client-takeover
```

The initial milestone does not advertise or certify Dynamic Client Registration, Implicit, Hybrid, Logout, FAPI, Federation, or eKYC profiles.

## Required Preparation

- Deploy the dedicated certification environment using your private operational deployment assets.
- Seed certification users and clients with `OPENIDENTITYSTACK_SEED_PROFILE=certification`.
- Configure the OpenID Foundation alias in `Seed__Certification__Alias`.
- Verify the public discovery document.
- Verify the JWKS endpoint.
- Verify login works for the certification users.
- Register the OpenID Foundation redirect URI for the chosen alias:

```text
https://www.certification.openid.net/test/a/<ALIAS>/callback
```

When using the staging conformance suite, also register:

```text
https://staging.certification.openid.net/test/a/<ALIAS>/callback
```

## Suggested Suite Configuration

- Server metadata discovery URL: `https://<public-issuer-host>/.well-known/openid-configuration`
- Client authentication method for `oidf-code-client`: `client_secret_basic`
- Client authentication method for `oidf-code-client-post`: `client_secret_post`
- Use PKCE where the suite asks for it.
- Use the dedicated certification test users only.

## Manual-Review Tests

Five Basic OP modules cannot complete on their own. They park in `WAITING`
until a human confirms what the provider displayed, and their ceiling is
`REVIEW`, which is certifiable. **Do not chase `PASSED` on these — it is
unreachable by design.** Config OP pauses for nothing.

The suite is the system of record for this evidence: screenshot what the
provider rendered and **upload it on the test's log detail page**, so it becomes
part of the test log and travels inside the exported results ZIP. Nothing is
copied into this repository.

### These five are excluded from automated sweeps

The Playwright runner drives browser URLs but has no way to acknowledge a
review, so a manual-review module stays `WAITING` until the per-test deadline
expires. That is worse than slow: a test still holding the alias when the next
one starts is killed by the suite with an alias conflict, which silently
corrupts every result after it.

The runner therefore **skips all five by default** and names them on startup.
Two ways to drive them anyway:

- `--only <module>` runs exactly the modules named, overriding the exclusion.
  This is the right way to work one review test at a time.
- `--include-manual-review` puts them back into a full sweep. Only useful with a
  human watching, ready to complete each review before the deadline.

Either way the runner **requires `--headed`** and **pauses at the OP's login
form**, printing a prompt and waiting for Enter before submitting. Both are
necessary rather than convenient: for `oidcc-prompt-login` and `oidcc-max-age-1`
the evidence *is* the fresh login form, so a headless browser has nothing to
photograph and an immediate submit destroys the only thing worth capturing.

### What correct behaviour looks like

Verified against the local stack before the hosted run
([#310](https://github.com/Tjeerd-menno/open-identity-stack/issues/310)), so a
reviewer can tell a genuine pass from a plausible-looking failure.

| Test | Correct provider behaviour | What the screenshot must show |
|---|---|---|
| `oidcc-response-type-missing` | Rejects the request; **no redirect issued** — a request missing `response_type` is not safe to redirect | Provider error page, address bar still on `/connect/authorize` |
| `oidcc-ensure-registered-redirect-uri` | Rejects the request; **no redirect issued** — the `redirect_uri` is not registered, so it must never be used | Provider error page, address bar still on `/connect/authorize` |
| `oidcc-ensure-request-object-with-redirect-uri` (reject path) | Answers `request_not_supported`; **no redirect issued** — request objects are rejected, so a `redirect_uri` inside the object cannot take effect | Provider error page, address bar still on `/connect/authorize` |
| `oidcc-prompt-login` | Forces a **fresh login** even though a session is already live | Login form, address bar showing `prompt=login` |
| `oidcc-max-age-1` | Forces a **fresh login** even though a session is already live | Login form, address bar showing `max_age=1` |

For the first three, the evidence is a *negative* — that no redirect happened.
A cropped screenshot of an error page cannot show that. The address bar is the
proof, so it is captured deliberately.

### Capture rules

- **Capture the full browser window, including the address bar.** The authorize
  URL carries `client_id`, `redirect_uri`, `state` and `nonce` — none of them
  secrets, all of them needed to judge the test.
- **If the visible URL contains `code=`, discard the screenshot and retake it.**
  That is a callback, not a provider error page: the wrong moment was captured,
  and an authorization code is the one value in this flow worth protecting.
- **No devtools or network panel open** in the capture.
- Upload each screenshot to its own test's log detail page before moving on. A
  test whose evidence is captured after the fact cannot be matched back
  reliably.
- The human driving the suite performs the capture — agents never handle OIDF
  credentials, and the capture happens in the authenticated suite session.

[`oidf-negative-review-automation-plan.md`](oidf-negative-review-automation-plan.md)
is **superseded for manual runs**, including its `review.json` manifest: a local
manifest would be a second copy of evidence that the actual reviewer never
opens. The plan stands only as a record of intent for automation, which is out
of scope for the certification effort.

## Result Handling

- All required tests must finish successfully.
- `FAILED` or `INTERRUPTED` results block certification.
- Review every warning and document why it is acceptable before submission.
- Export result ZIP files after the final run.
- Do not submit or archive logs containing reusable secrets.

### Runner exit codes

The runner's exit code is the quick check; the results JSON carries the detail.

| Code | Meaning |
|---|---|
| `0` | Every module reached a verdict and none `FAILED`. |
| `1` | At least one module `FAILED`. |
| `2` | At least one module stalled. **Discard the run** — later results may be corrupted by alias conflicts. |
| `3` | At least one module produced no result at all (failed to start, `INTERRUPTED`, or no verdict). Evidence has holes in it. |

Codes `2` and `3` are not "mostly fine". A missing verdict is missing evidence,
which blocks certification exactly as a `FAILED` does.

### Credentials

The runner reads the OP password from the **`CONFORMANCE_PASSWORD` environment
variable only**. It refuses a `--password` argument: a sweep runs for tens of
minutes, and its command line is readable by other processes for the whole of
that time and is persisted to shell history afterwards.

It also refuses to type the password into any page not served by the OP named in
the plan config's `server.discoveryUrl`. The suite chooses the URL each flow
starts from, and the runner disables certificate validation to accommodate the
local self-signed certificate, so the origin check is what stands between a
misconfigured `--suite` and a leaked certification credential.
- Deactivate or rotate certification user passwords and client secrets after exporting final evidence.
