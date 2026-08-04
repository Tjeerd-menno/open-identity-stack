# OIDF Negative-Case Review Automation Plan

> **Status: record of intent only — and its starting target no longer exists.**
>
> Building this automation is out of scope for the certification effort, and
> [`run-oidf-conformance-suite.md`](run-oidf-conformance-suite.md) supersedes it
> for manual runs, including the `review.json` manifest below.
>
> The Initial Target, `oidcc-response-type-missing`, **stopped being a
> review-pause test** once authorization errors began redirecting to a validated
> `redirect_uri` ([#331](https://github.com/Tjeerd-menno/open-identity-stack/pull/331)):
> the suite verifies it automatically and it now passes in an ordinary sweep.
> Anyone reviving this plan needs a new starting target from the current
> manual-review list, not this one.
>
> The classification step below is the part that aged well — it already treats
> "redirect to the suite callback with `error`" as the automatic case, which is
> precisely what happened.

## Purpose

Some OpenID Foundation conformance tests intentionally send invalid authorization requests. When the provider correctly displays a local error page instead of redirecting to an untrusted or incomplete callback, the suite can pause for manual review.

This plan describes how to make those review steps repeatable without storing test secrets or depending on tribal knowledge.

## Initial Target

Automate evidence capture for the OpenID Connect Core Basic OP negative authorization tests, starting with:

- `oidcc-response-type-missing`

The current expected provider behavior is a protocol error response for a request missing `response_type`, with no redirect unless the request is safe to redirect.

## Automation Stages

1. Read the conformance log page and capture:
   - plan id
   - test id
   - test name
   - browser interaction URL
   - expected review status

2. Open the browser interaction URL in an isolated browser context.

3. Classify the result:
   - Redirect to the conformance suite callback with `error`: allow the suite to process it automatically.
   - Provider-hosted error page or plain error response: capture review evidence.
   - Unexpected success page or login page: fail the automation and record diagnostics.

4. Capture evidence for provider-hosted errors:
   - screenshot
   - final URL
   - HTTP status if available
   - visible error text
   - timestamp
   - OpenIdentityStack image tag or commit SHA

5. Upload or attach the screenshot to the conformance review step when the suite exposes an upload control.

6. Record the outcome in a local review manifest under:

```text
artifacts/oidf-conformance/<plan-id>/<test-id>/review.json
```

## Evidence Rules

The automation must not persist:

- client secrets
- authorization codes
- access tokens
- refresh tokens
- ID tokens
- session cookies

Screenshots should contain only the provider error page or the conformance review page. Network logs must be redacted before storage.

## Suggested Manifest

```json
{
  "plan_id": "jTX15RwPg2ta5",
  "test_id": "SMmyTY8GaPOZbM6",
  "test_name": "oidcc-response-type-missing",
  "issuer": "https://<public-issuer-host>",
  "result": "review_required",
  "observed_error": "invalid_request",
  "observed_error_description": "The mandatory 'response_type' parameter is missing.",
  "evidence": {
    "screenshot": "screenshot.png",
    "final_url": "https://<public-issuer-host>/connect/authorize?...",
    "captured_at": "2026-05-17T00:00:00Z"
  }
}
```

## Implementation Approach

Use Playwright from the Codex browser automation environment for the first implementation because it can reuse the signed-in conformance browser session when needed.

Later, extract the flow into a small script that accepts:

```text
--plan-id <plan-id>
--log-id <test-id>
--artifact-dir artifacts/oidf-conformance
```

The script should be idempotent. If evidence already exists for a test id, it should verify the files and manifest instead of overwriting them silently.

## Review Catalog

Track each paused negative test in this table as it is encountered.

| Test | Expected provider behavior | Evidence required |
|---|---|---|
| ~~`oidcc-response-type-missing`~~ | **No longer pauses.** Redirects `error=invalid_request` with `state` to the validated `redirect_uri`; the suite verifies this automatically. | None — it is a normal sweep module |

The live catalog of tests that *do* pause is the manual-review table in
[`run-oidf-conformance-suite.md`](run-oidf-conformance-suite.md).

## Definition of Done

- Each review-pause test has a recorded expected behavior.
- Evidence capture is repeatable.
- Artifacts avoid reusable secrets and tokens.
- New review failures are added to the catalog before being resolved.
- Once the script exists, CI can run it in dry-run mode against stored sample pages.
