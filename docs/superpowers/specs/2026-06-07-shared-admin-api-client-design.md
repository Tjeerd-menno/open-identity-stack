# Shared Admin API Client Design

## Context

`CONTEXT.md` defines a Shared Admin API Client at `src/frontend-packages/admin-api-client`, but the module does not exist yet. Management Web currently uses `src/OpenIdentityStack.ManagementWeb/src/lib/admin-api.ts`, a `fetch`-based request module. Management Web currently uses `src/OpenIdentityStack.ManagementWeb/src/lib/api/client.ts`, an Axios-backed `ApiClient`.

This creates two interfaces over the same Admin API seam. Error normalization, token injection, unauthorized handling, query parameter serialization, base URL handling, and empty-response behavior can drift between Management Web and Management Web. ADR-0003 accepts a unified release train for breaking Admin API changes, so the client seam should concentrate cross-UI Admin API behavior in one module.

## Goal

Make the Shared Admin API Client real without migrating every feature endpoint function in the first slice.

The first slice deepens the low-level Admin API seam while preserving the existing UI-local feature module imports. Domain endpoint functions can move into the shared package in later slices after the request/error behavior is aligned.

## Non-Goals

- Do not migrate every Management Web or Management Web feature endpoint function.
- Do not introduce UI-specific auth behavior into the shared implementation.
- Do not change backend Admin API routes or response contracts.
- Do not unify Management Web and Management Web visual systems, navigation, or deployment topology.
- Do not introduce a broad frontend framework package beyond the Admin API client seam.

## Architecture

Create `src/frontend-packages/admin-api-client` as a small TypeScript package.

The shared package owns:

- request construction
- query parameter serialization
- JSON request body handling
- JSON and empty response parsing
- Bearer token injection
- unauthorized callback invocation on HTTP 401
- Admin API error normalization
- shared request and response support types

The shared package does not own:

- how either UI obtains an access token
- how either UI logs out or redirects after unauthorized responses
- Vite runtime configuration
- UI notifications
- feature slice query invalidation

## Interface

The package exports:

```ts
createAdminApiClient(options)
```

`options` contains:

- `baseUrl: string | (() => string)`
- `getAccessToken?: () => Promise<string | null>`
- `onUnauthorized?: () => void`

The returned client exposes:

- `request<T>(path, options?, params?)`
- `get<T>(path, params?)`
- `post<T>(path, body?)`
- `put<T>(path, body?)`
- `patch<T>(path, body?)`
- `delete<T>(path, params?)`

The package also exports:

- `ApiError`
- `createApiError`
- `formatApiError`
- `isApiError`
- `PaginatedResponse<T>`

The shared implementation uses `fetch`. This avoids moving Management Web's Axios dependency into the shared module and keeps the interface based on browser platform behavior.

## UI Adapters

Management Web keeps `src/OpenIdentityStack.ManagementWeb/src/lib/admin-api.ts` as a compatibility adapter. It delegates to the shared package and preserves current exports such as `request`, `setAccessTokenProvider`, `setUnauthorizedHandler`, `getApiErrorMessage`, `isApiError`, `PaginatedResponse`, and `listRoles`.

Management Web keeps `src/OpenIdentityStack.ManagementWeb/src/lib/api/client.ts` as a compatibility adapter. It preserves the current `apiClient.get/post/put/patch/delete`, `setTokenProvider`, and `setLogoutHandler` interface while delegating behavior to the shared package.

This keeps feature slices stable while moving the actual Admin API seam into one module.

## Package Integration

The preferred package location is `src/frontend-packages/admin-api-client`, matching `CONTEXT.md`.

The implementation should use the least disruptive package wiring that works with both existing Vite/TypeScript apps:

- prefer local package dependency wiring if it works cleanly for both UI projects
- otherwise use TypeScript/Vite aliasing for the first slice

Do not add a repo-root npm workspace in this slice unless the local package cannot be consumed reliably without it.

## Error Handling

The shared module normalizes error payloads into `ApiError`.

It should support current backend shapes:

- RFC-style problem details: `type`, `title`, `status`, `detail`, `errors`
- current simple error payloads: `error`, `message`
- network failures where no HTTP response is available
- unknown failures with a useful fallback message

Management Web and Management Web adapters format these errors through the shared `formatApiError` helper.

## Testing

Use TDD for implementation.

Shared package tests cover:

- appends query parameters and omits undefined values
- injects Bearer token when `getAccessToken` returns a token
- does not add Authorization when no token is available
- serializes JSON request bodies
- parses JSON responses
- returns `undefined` for HTTP 204 and empty bodies
- calls `onUnauthorized` for HTTP 401
- normalizes problem-details errors
- normalizes simple `{ error, message }` errors
- reports network failures as `ApiError`

UI adapter tests cover:

- Management Web keeps its existing exported functions and behavior
- Management Web keeps its existing `apiClient` interface
- both adapters wire token providers and unauthorized handlers into the shared module

Existing feature endpoint tests should not need large rewrites in this slice.

## Rollout

1. Add shared package tests and verify they fail because the package does not exist.
2. Implement the shared package.
3. Migrate Management Web's low-level `admin-api.ts` adapter.
4. Migrate Management Web's low-level `api/client.ts` adapter.
5. Run focused package/client tests.
6. Run both UI type checks if package wiring changes TypeScript resolution.

## Risks

The main risk is package wiring. The repo currently has separate npm projects for Management Web and Management Web and no repo-root npm workspace. Keep wiring minimal in this slice so the architecture improves without forcing a broad dependency-management migration.

The second risk is behavior drift during adapter migration. Preserve the existing adapter interfaces first, then move feature endpoint functions in later slices only after tests prove both UIs agree on request/error semantics.

