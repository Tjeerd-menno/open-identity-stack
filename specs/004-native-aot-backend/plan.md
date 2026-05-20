# Implementation Plan: Native AOT Backend Deployment

**Branch**: `004-native-aot-backend` | **Date**: 2026-05-20 | **Spec**: `plan-only`
**Input**: Planning request to support Native AOT deployment for `OpenIdentityStack.Api`

## Summary

Enable Native AOT deployment support for the OpenIdentityStack backend service, with `OpenIdentityStack.Api` as the AOT target. Preserve the existing OAuth 2.0/OpenID Connect, admin API, federation, health, and operational behavior while changing unsupported runtime patterns to AOT-compatible alternatives.

`OpenIdentityStack.DbMigrator` remains a separate migration and seeding executable. Database migrations are not part of the AOT API runtime path, and production deployments must continue to run migration/seeding before starting the API.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Runtime Target**: `src/OpenIdentityStack.Api/OpenIdentityStack.Api.csproj`
**Adjacent Deployment Components**:
- `src/OpenIdentityStack.DbMigrator` for EF migrations and seed data
- `src/OpenIdentityStack.AppHost` for Aspire local orchestration
- `src/OpenIdentityStack.Api/Dockerfile` for the backend service image

**Primary Dependencies**:
- ASP.NET Core 10.0
- EF Core 10.0 with Npgsql/PostgreSQL
- OpenIddict 7.5.0
- Microsoft.AspNetCore.OpenApi and Scalar.AspNetCore
- System.Text.Json
- Aspire service defaults and PostgreSQL health checks

**Target Publish RIDs**:
- `linux-x64` for container deployment
- `win-x64` for Windows service/native executable deployment

**Native AOT Constraints**:
- AOT warnings such as `IL2026`, `IL3050`, and trim analysis warnings are release blockers.
- Runtime code generation, reflection-based discovery, unsupported MVC/Razor patterns, and reflection-based JSON serialization must be removed or guarded from the AOT service path.
- EF Core migrations must stay outside the AOT API runtime and run through `DbMigrator` or migration bundles.

## Current AOT Smoke-Test Findings

A local smoke publish using:

```powershell
dotnet publish src\OpenIdentityStack.Api\OpenIdentityStack.Api.csproj -c Release -r win-x64 -p:PublishAot=true -p:EnforceCodeStyleInBuild=false
```

failed before native linking. The first blockers are:

| Area | Finding | Required Direction |
|------|---------|--------------------|
| JSON enum serialization | `JsonStringEnumConverter` is not AOT-safe | Replace with generic enum converters or explicit string DTO properties |
| JSON HTTP helpers | `ReadFromJsonAsync<T>` and `JsonSerializer.Serialize/Deserialize<T>` use reflection-based metadata | Use source-generated `JsonSerializerContext` and `JsonTypeInfo` overloads |
| EF Core DbContext | `DbContext` and runtime model construction produce trim/AOT warnings | Add EF compiled model/AOT workflow and eliminate dynamic model discovery |
| EF model configuration | `ApplyConfigurationsFromAssembly` is trim-unsafe | Explicitly apply every entity configuration |
| EF migration helpers | `EnsureCreatedAsync` is marked unsupported for AOT | Keep schema creation/migration helpers out of the AOT API publish path |
| Query composition | `AsQueryable()` over in-memory collections is flagged | Rewrite to direct LINQ over concrete collections or database-backed queries |
| MVC/Razor/controllers | `AddControllersWithViews`, `MapControllers`, `AddRazorPages`, and Razor views are not the desired AOT surface | Move to Minimal APIs or compiled/static endpoint equivalents |
| OpenAPI/Scalar | Development-only OpenAPI UI may not be AOT-safe | Guard or remove from AOT publish path if analyzer warnings remain |

## Implementation Plan

### 1. Add AOT Publish Configuration

- Add an opt-in Native AOT publish profile or MSBuild property group for `OpenIdentityStack.Api`.
- Configure publish commands for `linux-x64` and `win-x64`.
- Treat AOT and trim warnings as errors for the AOT profile.
- Keep regular Debug/Release builds unchanged for local development and existing tests.
- Add a CI smoke publish step that runs the AOT publish without producing release artifacts.

### 2. Convert HTTP Surface to AOT-Compatible Endpoints

- Replace MVC/controller/Razor registration in `Program.cs`:
  - Remove `AddControllersWithViews()`, `MapControllers()`, `AddRazorPages()`, and `MapRazorPages()` from the AOT runtime path.
  - Preserve all current routes and status-code behavior through Minimal APIs or explicit endpoint mappers.
- Convert `/connect/*`, `/Account/*`, logout, callback, and test seeding routes to endpoint mapping classes.
- Replace Razor login/check-session rendering with compiled HTML helpers or static assets served by the API.
- Keep antiforgery, rate limiting, cookie auth, CORS, forwarded headers, HSTS, security headers, and health endpoints behaviorally equivalent.
- Remove anonymous response payloads from endpoints and replace them with named DTO records/classes.

### 3. Add Source-Generated JSON Metadata

- Add a central API JSON source-generation context for:
  - all admin API request and response DTOs
  - OIDC/account/federation request and response DTOs
  - validation/problem/error DTOs
  - collection response shapes
  - OpenID discovery, token, userinfo, and back-channel logout payloads used by infrastructure code
- Wire MVC/minimal API JSON options to use the generated context resolver.
- Replace non-generic `JsonStringEnumConverter` with AOT-safe generic converters or explicit string mapping.
- Replace all `JsonSerializer` and `System.Net.Http.Json` calls with source-generation overloads.
- Add tests around representative serialization cases, including enum/string behavior and nested collection responses.

### 4. Make EF Core Usage AOT-Friendly

- Add EF Core AOT/compiled-model support for `OpenIdentityStackDbContext`.
- Replace `ApplyConfigurationsFromAssembly` with explicit `modelBuilder.ApplyConfiguration(new ...)` calls for each entity configuration.
- Keep migrations and seed execution in `OpenIdentityStack.DbMigrator`; do not call migration APIs from `OpenIdentityStack.Api`.
- Remove or guard schema helper methods in `ServiceDefaults` that call `EnsureCreatedAsync` so they do not participate in AOT API publishing.
- Rewrite flagged `AsQueryable()` patterns in repositories to use direct collection filtering or database-backed queryables.
- Validate PostgreSQL and test SQLite paths after model changes.

### 5. Verify OpenIddict and Federation Compatibility

- Keep OpenIddict as the OIDC/OAuth engine unless analyzer or runtime validation proves an incompatibility.
- After framework-level AOT blockers are resolved, rerun AOT publish and address OpenIddict-specific trim/AOT warnings.
- Validate server endpoints: discovery, JWKS, authorization, token, userinfo, revocation, introspection, logout, and session management.
- Validate dynamic upstream provider registration. If `OpenIdConnectHandler` or dynamic scheme setup remains incompatible, replace only the upstream federation handler path with an AOT-safe custom redirect/callback/token/userinfo implementation.
- Preserve certificate loading, data-protection persistence, internal token claim trimming, service-account validation, and authorization error redirect behavior.

### 6. Update Deployment Artifacts

- Update `src/OpenIdentityStack.Api/Dockerfile` to publish with `PublishAot=true`.
- Use a `runtime-deps` or chiseled final image instead of the ASP.NET runtime image.
- Change the container entrypoint from `dotnet OpenIdentityStack.Api.dll` to the native executable.
- Keep `DbMigrator` image/runtime unchanged unless a separate migration artifact decision is made.
- Update Aspire publish/deployment expectations so the migrator runs first and the API starts from the AOT artifact/container in deployment validation.
- Preserve Windows service install scripts, adjusting the executable name/path only if the native artifact name changes.

### 7. Update Documentation and Release Checks

- Document Native AOT deployment in installation and operations docs.
- Make clear that AOT support applies to the API service, not the migrator.
- Document RID-specific publish commands for `linux-x64` and `win-x64`.
- Document the migration-before-start requirement.
- Document Podman container build/run examples for the AOT image.
- Add troubleshooting notes for AOT publish warnings, missing generated JSON metadata, EF compiled model drift, and missing certificates/configuration.

## Public Interfaces

- No intentional HTTP API route or wire-contract changes.
- OIDC/OAuth endpoint URLs remain stable:
  - `/.well-known/openid-configuration`
  - `/.well-known/jwks`
  - `/connect/authorize`
  - `/connect/token`
  - `/connect/userinfo`
  - `/connect/introspect`
  - `/connect/revoke`
  - `/connect/logout`
- Admin API routes remain stable under `/api/admin/*`.
- Deployment interface gains Native AOT publish and container variants.
- Runtime container entrypoint changes to the native executable.
- Migration/seeding remains a separate operational step before API startup.

## Test Plan

### Build and Publish Validation

- `dotnet restore OpenIdentityStack.slnx`
- `dotnet build OpenIdentityStack.slnx --no-restore`
- `dotnet publish src/OpenIdentityStack.Api/OpenIdentityStack.Api.csproj -c Release -r linux-x64 -p:PublishAot=true`
- `dotnet publish src/OpenIdentityStack.Api/OpenIdentityStack.Api.csproj -c Release -r win-x64 -p:PublishAot=true`
- Both AOT publishes must complete with no trim/AOT warnings.

### Automated Tests

- Run fast unit suites for Domain, Application, Infrastructure, and API.
- Run API and contract test modules against the AOT-published service where feasible.
- Prioritize coverage for:
  - OIDC discovery and JWKS
  - authorization code flow
  - token flow
  - userinfo
  - logout/session management
  - service-account/client-credentials flow
  - admin CRUD endpoints
  - federation provider management

### Deployment Smoke Tests

- Start PostgreSQL.
- Run `OpenIdentityStack.DbMigrator`.
- Start the AOT API binary/container.
- Verify:
  - `/health`
  - `/alive`
  - `/.well-known/openid-configuration`
  - `/connect/token`
  - interactive login
  - AdminWeb API calls
- Build and run the AOT API container with Podman.
- Validate mounted certificate/configuration paths in a production-like run.

### Documentation Validation

- `python -m mkdocs build --strict`

## Assumptions

- "Backend service" means `OpenIdentityStack.Api`.
- `OpenIdentityStack.DbMigrator` remains non-AOT unless explicitly requested later.
- Native AOT support means feature parity for the API service, not a reduced mode that disables interactive login, OpenIddict endpoints, or federation.
- Target deployment RIDs are `linux-x64` and `win-x64`.
- EF Core Native AOT support is high risk enough that zero publish warnings plus runtime flow validation is the acceptance bar.
- Only `plan.md` is added for this spec; `spec.md`, `tasks.md`, contracts, and implementation patches are out of scope for this change.
