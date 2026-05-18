# OIDF Negative-Case Review Automation Plan

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
| `oidcc-response-type-missing` | Reject missing `response_type` with `invalid_request` or `unsupported_response_type`; local error display is acceptable when redirect safety is not established. | Screenshot or callback evidence |

## Definition of Done

- Each review-pause test has a recorded expected behavior.
- Evidence capture is repeatable.
- Artifacts avoid reusable secrets and tokens.
- New review failures are added to the catalog before being resolved.
- Once the script exists, CI can run it in dry-run mode against stored sample pages.
