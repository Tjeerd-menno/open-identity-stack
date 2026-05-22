# Data Model: OIDC Token Introspection Endpoint

## Introspection Request

Represents an API caller asking IAM to validate a token.

**Fields**
- `token`: Required. Access token submitted using form-encoded OAuth introspection request semantics.
- `token_type_hint`: Optional. Caller hint for token type.
- Authenticated caller identity: Required. Resolved by OpenIddict client authentication.

**Validation Rules**
- Caller must authenticate before token metadata is disclosed.
- Missing or invalid token must not return subject or permission metadata.
- Caller must have permission to use the introspection endpoint.

## Introspection Response

Represents IAM's response for a token.

**Fields**
- `active`: Required boolean. Indicates token activity.
- `sub`: Optional string. Present for active tokens with a subject.
- `permissions`: Optional array of strings. Caller-filtered fine-grained permissions.

**Validation Rules**
- `permissions` must only contain values relevant to the authenticated requesting API.
- Duplicate permissions must be emitted once.
- Inactive or invalid token responses must not disclose user permissions.

## Permission

Fine-grained authorization value used by APIs for local authorization decisions.

**Fields**
- `value`: String permission key, expected to be service-prefixed, for example `patient-api:read-patients`.
- `service boundary`: The prefix before the first colon identifies the API/service boundary.

**Validation Rules**
- Only permissions whose service boundary matches the requesting client identifier are returned.
- Wildcard administrator permission remains internal to IAM decisions unless explicitly allowed by the filtering rules.

## Requesting API

Authenticated client application calling introspection.

**Fields**
- `client_id`: Required authenticated client identifier.
- Endpoint permission: Must be allowed to call `/connect/introspect`.

**Relationships**
- Defines the service boundary used to filter returned permissions.
- May receive short-lived cached introspection responses on its own side.

## Current User Permission Source

The current authorization state for a user token.

**Fields**
- Subject/user id from the active token.
- Effective roles from direct and group-mapped assignments.
- Role permissions.

**State Transitions**
- Role added, removed, enabled, disabled, or permission changed: next successful introspection should reflect the changed current permission set.
- User disabled or token revoked: OpenIddict/token validation should report inactive or reject without permission metadata.
