# Quickstart: OIDC Token Introspection Endpoint

## Prerequisites

- .NET 10 SDK
- Restored solution dependencies
- Existing OpenIdentityStack test infrastructure

## Build

```powershell
dotnet build OpenIdentityStack.slnx --no-restore
```

## Focused Verification

Route registration:

```powershell
dotnet test --project tests/OpenIdentityStack.Api.UnitTests/OpenIdentityStack.Api.UnitTests.csproj --filter-class OpenIdentityStack.Api.UnitTests.Endpoints.OidcControllerRouteTests --no-restore
```

Controller behavior:

```powershell
dotnet test --project tests/OpenIdentityStack.Api.Tests/OpenIdentityStack.Api.Tests.csproj --filter-method OpenIdentityStack.Api.Tests.Authentication.AuthorizationControllerTests.Introspect_WhenAuthFails_ReturnsChallenge --filter-method OpenIdentityStack.Api.Tests.Authentication.AuthorizationControllerTests.Introspect_ReturnsActiveSubjectAndCallerFilteredFreshPermissions --no-restore
```

OpenIddict response enrichment:

```powershell
dotnet test --project tests/OpenIdentityStack.Infrastructure.Tests/OpenIdentityStack.Infrastructure.Tests.csproj --filter-class OpenIdentityStack.Infrastructure.Tests.Identity.IntrospectionPermissionsHandlerTests --no-restore
```

## Manual Smoke Shape

1. Register or seed a confidential API client with introspection endpoint permission.
2. Issue an access token for a user with permissions in multiple service namespaces.
3. Submit a form-encoded `POST /connect/introspect` request authenticated as the API client.
4. Verify the response contains `active: true`, the token subject, and only permissions for the caller service.
5. Change the user's role permissions and repeat introspection to confirm current authorization data is reflected.
