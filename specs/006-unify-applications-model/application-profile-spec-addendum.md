# Spec Addendum: Rename Application Profile to Application Profile

**Feature:** Unified Applications Model  
**PR:** `#120` — Unify applications model  
**Addendum status:** Proposed  
**Decision:** Rename the product/domain concept `ApplicationProfile` to `ApplicationProfile`, and expose it as `profile` in API contracts and Management Web forms.

---

## 1. Summary

The unified `Application` aggregate should keep the name **Application**. However, the current `ApplicationProfile` name creates avoidable confusion with OpenID Connect and OpenIddict protocol terminology.

In OAuth 2.0 and OpenID Connect, the protocol entity is a **client**. OpenID Connect Dynamic Client Registration also has an `application_type` metadata concept that is narrower than the Open Identity Stack product classification. The PR currently uses `ApplicationProfile` for product choices such as `MachineToMachine`, `SinglePage`, `Device`, and `Custom`. Those choices are not the same as the protocol-level `application_type`.

Therefore, the PR should rename:

```text
ApplicationProfile  -> ApplicationProfile
Application.Type -> Application.Profile
API JSON: type   -> profile
UI dropdown: Application Profile -> Profile
```

The Management Web dropdown should be labelled **Profile** and should select the product profile that controls the allowed OAuth/OIDC configuration surface.

---

## 2. Terminology Decision

### 2.1 Accepted Terms

| Term | Layer | Meaning |
|---|---|---|
| `Application` | Domain / API / UI | Administrator-managed OAuth 2.0 / OpenID Connect client application registration. |
| `ApplicationProfile` | Domain / API / UI | Product profile describing how the application obtains tokens and which configuration options are available. |
| `OAuthClientType` | Domain / API | Protocol client profile: `Public` or `Confidential`. |
| `client_id` / `ClientId` | Protocol / Domain / API | OAuth client identifier. |
| `OpenIddictApplicationDescriptor.ApplicationProfile` | Infrastructure adapter only | OpenIddict/OIDC protocol-level application profile. This remains unchanged because it belongs to OpenIddict. |

### 2.2 Rejected Terms

| Term | Reason |
|---|---|
| `ApplicationProfile` | Too easily confused with OIDC Dynamic Client Registration `application_type` and OpenIddict descriptor terminology. |
| `ClientApplicationProfile` | Still suggests protocol-level client metadata instead of product-level behavior. |
| `ApplicationKind` | Acceptable, but less explicit than `Profile`; “profile” better expresses a bundle of defaults, constraints, and available options. |
| `ApplicationCategory` | Sounds like reporting or taxonomy, not behavior. |

### 2.3 Product Definition

An **Application Profile** is a product-level configuration profile that determines:

- default `OAuthClientType`;
- allowed OAuth grant types;
- redirect URI requirements;
- whether secrets/certificates are allowed;
- whether PKCE is required;
- whether consent is relevant;
- which fields are shown or hidden in Management Web.

It is not a protocol identifier and it is not projected directly as-is to OpenIddict’s `ApplicationProfile`.

---

## 3. Application Profiles

The supported values remain the same; only the concept name changes.

```csharp
namespace OpenIdentityStack.Domain.Applications;

/// <summary>
/// Product profile for a registered OAuth 2.0 / OpenID Connect application.
/// </summary>
public enum ApplicationProfile
{
    MachineToMachine = 0,
    Web = 1,
    SinglePage = 2,
    Native = 3,
    Device = 4,
    Custom = 5
}
```

### 3.1 Profile Meanings

| Profile | Meaning | Default OAuth Client Type | Primary Grant Types |
|---|---|---|---|
| `Web` | Server-side web application or BFF that can protect credentials. | `Confidential` | `authorization_code`, optional `refresh_token` |
| `SinglePage` | Browser-based frontend that cannot protect shared secrets. | `Public` | `authorization_code`, optional `refresh_token` |
| `Native` | Mobile or desktop app installed on a user device. | `Public` | `authorization_code`, optional `refresh_token` |
| `MachineToMachine` | Backend workload calling APIs without an end user. | `Confidential` | `client_credentials` |
| `Device` | Input-constrained device using device authorization flow. | `Public` by default | `urn:ietf:params:oauth:grant-type:device_code`, optional `refresh_token` |
| `Custom` | Explicit escape hatch for advanced deployments. | Explicitly supplied | Explicitly supplied |

---

## 4. Scope of the Change

This addendum applies to the unified Applications work in PR `#120`.

### 4.1 In Scope

Rename the product/domain concept everywhere it appears as part of the unified Applications feature:

```text
ApplicationProfile.cs
Application.Type
ApplicationDetails.Type
ApplicationSummary.Type
CreateApplicationCommand.Type
ConfigureApplicationOAuthCommand.Type
CreateApplicationRequest.Type
ConfigureApplicationOAuthRequest.Type
ApplicationCreatedResponse.Type
ApplicationResponse.Type
ApplicationListItemResponse.Type
ListApplications type query parameter
Management Web ApplicationProfile profile
Management Web form field type
OpenAPI type schema/property
Contract test payloads
Documentation and Spec Kit artifacts
Database column/index names introduced by this PR
```

### 4.2 Out of Scope

Do not rename protocol-level terms:

```text
OAuthClientType
ClientId
client_id
AllowedGrantTypes
OpenIddictApplicationDescriptor.ApplicationProfile
OpenIddictConstants.ApplicationProfiles.Web
OpenIddictConstants.ApplicationProfiles.Native
```

Do not introduce a REST API / resource server profile in this addendum. REST APIs remain a separate future concept such as `ApiResource` or `ProtectedResource`.

---

## 5. Domain Model Changes

### 5.1 Rename Enum

Rename:

```text
src/OpenIdentityStack.Domain/Applications/ApplicationProfile.cs
```

to:

```text
src/OpenIdentityStack.Domain/Applications/ApplicationProfile.cs
```

Replace:

```csharp
public enum ApplicationProfile
{
    MachineToMachine = 0,
    Web = 1,
    SinglePage = 2,
    Native = 3,
    Device = 4,
    Custom = 5
}
```

with:

```csharp
public enum ApplicationProfile
{
    MachineToMachine = 0,
    Web = 1,
    SinglePage = 2,
    Native = 3,
    Device = 4,
    Custom = 5
}
```

### 5.2 Rename Aggregate Property

Replace:

```csharp
public ApplicationProfile Profile { get; private set; }
```

with:

```csharp
public ApplicationProfile Profile { get; private set; }
```

### 5.3 Rename Method Parameters

Replace the `type` parameter with `profile` in:

```text
Application.Create(...)
Application.ConfigureOAuth(...)
Application.CreateMachineToMachine(...)
Application.Validate(...)
ValidatedApplicationConfiguration
```

Example target shape:

```csharp
public static Result<Application> Create(
    string clientId,
    string displayName,
    string? description,
    ApplicationProfile profile,
    OAuthClientType clientType,
    IReadOnlyList<string> allowedGrantTypes,
    IReadOnlyList<string> allowedScopes,
    IReadOnlyList<string> redirectUris,
    IReadOnlyList<string> postLogoutRedirectUris,
    bool requirePkce,
    bool requireConsent,
    IDateTimeProvider dateTimeProvider)
{
    Result<ValidatedApplicationConfiguration> validation = Validate(
        clientId,
        displayName,
        description,
        profile,
        clientType,
        allowedGrantTypes,
        allowedScopes,
        redirectUris,
        postLogoutRedirectUris,
        requirePkce);

    if (validation.IsFailure)
    {
        return validation.Error;
    }

    ValidatedApplicationConfiguration values = validation.Value;

    var application = new Application(
        ApplicationId.NewId(),
        values.ClientId,
        values.DisplayName,
        values.Description,
        profile,
        clientType,
        values.AllowedGrantTypes,
        values.AllowedScopes,
        values.RedirectUris,
        values.PostLogoutRedirectUris,
        requirePkce,
        requireConsent,
        dateTimeProvider.UtcNow);

    application.RaiseDomainEvent(new ApplicationDomainEvents.ApplicationCreated(
        application.Id,
        application.ClientId,
        application.DisplayName,
        dateTimeProvider.UtcNow));

    return application;
}
```

### 5.4 Rename Validation Logic

Replace profile checks:

```csharp
if (type == ApplicationProfile.MachineToMachine)
{
    // ...
}

if (usesDeviceCode && type is not ApplicationProfile.Device and not ApplicationProfile.Custom)
{
    // ...
}
```

with:

```csharp
if (profile == ApplicationProfile.MachineToMachine)
{
    // ...
}

if (usesDeviceCode && profile is not ApplicationProfile.Device and not ApplicationProfile.Custom)
{
    // ...
}
```

### 5.5 Preserve Invariants

The rename must not change validation behavior. These invariants remain:

- `MachineToMachine` requires `OAuthClientType.Confidential`;
- `MachineToMachine` allows only `client_credentials`;
- `MachineToMachine` does not allow redirect URIs or post-logout redirect URIs;
- `client_credentials` requires a confidential client;
- `authorization_code` requires at least one redirect URI;
- public authorization-code clients require PKCE;
- `device_code` is allowed only for `Device` and `Custom`.

---

## 6. Application Layer Changes

### 6.1 Command Contracts

Rename command properties and constructor parameters.

Before:

```csharp
public sealed record CreateApplicationCommand(
    string ClientId,
    string DisplayName,
    string? Description,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent);
```

After:

```csharp
public sealed record CreateApplicationCommand(
    string ClientId,
    string DisplayName,
    string? Description,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent);
```

Apply the same pattern to:

```text
ConfigureApplicationOAuthCommand
ApplicationCommandResult
ApplicationDetails
ApplicationSummary
ListApplicationsQuery
ListApplicationsQueryHandler
```

### 6.2 Query Filtering

Rename the list filter:

```text
type -> profile
```

Target API handler signature:

```csharp
private static async Task<IResult> ListApplications(
    [FromServices] IListApplicationsQueryHandler listApplicationsQueryHandler,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] ApplicationProfile? profile = null,
    [FromQuery] ApplicationStatus? status = null,
    [FromQuery] OAuthClientType? clientType = null,
    [FromQuery] string? search = null,
    CancellationToken cancellationToken = default)
```

Target URL example:

```http
GET /api/admin/applications?profile=MachineToMachine&status=Active
```

---

## 7. API Contract Changes

### 7.1 Request DTOs

Before:

```csharp
public sealed record CreateApplicationRequest(
    string ClientId,
    string DisplayName,
    string? Description,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent,
    CreateInitialCredentialRequest? InitialCredential = null);
```

After:

```csharp
public sealed record CreateApplicationRequest(
    string ClientId,
    string DisplayName,
    string? Description,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent,
    CreateInitialCredentialRequest? InitialCredential = null);
```

Apply the same rename to:

```text
ConfigureApplicationOAuthRequest
ApplicationCreatedResponse
ApplicationResponse
ApplicationListItemResponse
```

### 7.2 JSON Contract

The JSON property must be `profile`.

Create request:

```json
{
  "clientId": "orders-worker",
  "displayName": "Orders Worker",
  "description": "Background processor for order workflows",
  "profile": "MachineToMachine",
  "clientType": "Confidential",
  "allowedGrantTypes": ["client_credentials"],
  "allowedScopes": ["orders.read", "orders.write"],
  "redirectUris": [],
  "postLogoutRedirectUris": [],
  "requirePkce": false,
  "requireConsent": false
}
```

List response item:

```json
{
  "id": "0b35f6b6-3bc6-44a2-9dd6-f490e6c9f3c7",
  "clientId": "orders-worker",
  "displayName": "Orders Worker",
  "profile": "MachineToMachine",
  "clientType": "Confidential",
  "status": "Active",
  "allowedGrantTypes": [],
  "credentialCount": 0,
  "createdAt": "2026-05-24T12:00:00Z",
  "modifiedAt": null
}
```

### 7.3 Backward Compatibility

Because PR `#120` is a pre-merge, pre-1.0 breaking change, the API does **not** need to accept both `type` and `profile`.

Reject or ignore legacy `type` consistently according to existing ASP.NET Core model binding behavior. The public OpenAPI contract should only document `profile`.

---

## 8. Infrastructure and OpenIddict Projection

### 8.1 Preserve OpenIddict Protocol Naming

Do not rename `OpenIddictApplicationDescriptor.ApplicationProfile`.

In the projection, the product profile should be explicitly mapped to the OpenIddict protocol application profile.

Target style:

```csharp
private static void ApplyApplication(
    OpenIddictApplicationDescriptor descriptor,
    DomainApplication application,
    string? clientSecret)
{
    descriptor.ClientId = application.ClientId;
    descriptor.DisplayName = application.DisplayName;

    descriptor.ClientType = application.ClientType == OAuthClientType.Confidential
        ? OpenIddictConstants.ClientTypes.Confidential
        : OpenIddictConstants.ClientTypes.Public;

    descriptor.ApplicationProfile = ToOpenIddictApplicationProfile(application.Profile);

    // Remaining projection unchanged.
}

private static string ToOpenIddictApplicationProfile(ApplicationProfile profile)
{
    return profile is ApplicationProfile.Native
        or ApplicationProfile.SinglePage
        or ApplicationProfile.Device
            ? OpenIddictConstants.ApplicationProfiles.Native
            : OpenIddictConstants.ApplicationProfiles.Web;
}
```

### 8.2 Avoid Ambiguous Local Names

Avoid local variables named `applicationProfile` for product profile values. Use:

```csharp
ApplicationProfile profile
string protocolApplicationProfile
```

This makes it clear when code is dealing with Open Identity Stack product semantics versus OpenIddict protocol metadata.

---

## 9. Persistence and Migration

### 9.1 EF Core Configuration

Rename database mapping introduced by this PR:

```text
Applications.Type -> Applications.Profile
```

If the PR migration has not shipped, update the migration and model snapshot directly so the initial unified table is created with `Profile`.

Target column:

```csharp
builder.Property(application => application.Profile)
    .HasConversion<int>()
    .IsRequired();
```

### 9.2 Database Column and Index Names

Use:

```text
Applications.Profile
IX_Applications_Profile
```

instead of:

```text
Applications.Type
IX_Applications_Type
```

### 9.3 Backfill Logic

Backfill behavior remains unchanged; only the target column/property name changes.

Examples:

```text
legacy Clients       -> Applications.Profile inferred from client/grant/redirect shape
legacy ServiceAccounts -> Applications.Profile = MachineToMachine
```

### 9.4 Migration Guidance

Because this is part of an open PR and the feature is not released:

- prefer editing/regenerating the PR migrations instead of adding a new rename migration;
- update `OpenIdentityStackDbContextModelSnapshot`;
- update SQL migration tests to assert `Profile`;
- avoid shipping a transient `Type` column that is immediately renamed.

If a developer already applied the PR migration locally, they should recreate the local development database or apply a one-off local migration. That should not shape the released migration history.

---

## 10. Management Web Changes

### 10.1 TypeScript Types

Rename:

```typescript
export const ApplicationProfile = {
  Web: 'Web',
  SinglePage: 'SinglePage',
  Native: 'Native',
  MachineToMachine: 'MachineToMachine',
  Device: 'Device',
} as const;
export type ApplicationProfile = typeof ApplicationProfile[keyof typeof ApplicationProfile];
```

to:

```typescript
export const ApplicationProfile = {
  Web: 'Web',
  SinglePage: 'SinglePage',
  Native: 'Native',
  MachineToMachine: 'MachineToMachine',
  Device: 'Device',
  Custom: 'Custom',
} as const;
export type ApplicationProfile = typeof ApplicationProfile[keyof typeof ApplicationProfile];
```

### 10.2 Application DTOs

Rename `type` to `profile`.

Before:

```typescript
export interface Application {
  id: string;
  clientId: string;
  displayName: string;
  description: string | null;
  type: ApplicationProfile;
  clientType: ApplicationClientType;
}
```

After:

```typescript
export interface Application {
  id: string;
  clientId: string;
  displayName: string;
  description: string | null;
  profile: ApplicationProfile;
  clientType: ApplicationClientType;
}
```

Apply the same rename to:

```text
ApplicationListItem
ApplicationListParams
CreateApplicationRequest
ConfigureApplicationOAuthRequest
ApplicationCreatedResponse
```

### 10.3 Form Schema

Before:

```typescript
const createSchema = z.object({
  type: z.enum(ApplicationProfile),
  clientType: z.enum(ApplicationClientType),
});
```

After:

```typescript
const createSchema = z.object({
  profile: z.enum(ApplicationProfile),
  clientType: z.enum(ApplicationClientType),
});
```

### 10.4 Dropdown UI

The dropdown should be labelled **Profile**.

Target UI shape:

```tsx
<FormField
  control={form.control}
  name="profile"
  render={({ field }) => (
    <FormItem>
      <FormLabel>Profile</FormLabel>
      <Select onValueChange={field.onChange} defaultValue={field.value}>
        <FormControl>
          <SelectTrigger>
            <SelectValue placeholder="Select profile" />
          </SelectTrigger>
        </FormControl>
        <SelectContent>
          <SelectItem value={ApplicationProfile.Web}>Web</SelectItem>
          <SelectItem value={ApplicationProfile.SinglePage}>Single Page</SelectItem>
          <SelectItem value={ApplicationProfile.Native}>Native</SelectItem>
          <SelectItem value={ApplicationProfile.MachineToMachine}>Machine-to-machine</SelectItem>
          <SelectItem value={ApplicationProfile.Device}>Device</SelectItem>
          <SelectItem value={ApplicationProfile.Custom}>Custom</SelectItem>
        </SelectContent>
      </Select>
      <FormDescription>
        Select how this application obtains tokens. The profile controls the available OAuth/OIDC options.
      </FormDescription>
      <FormMessage />
    </FormItem>
  )}
/>
```

### 10.5 Default Value

Before:

```typescript
type: ApplicationProfile.Web
```

After:

```typescript
profile: ApplicationProfile.Web
```

### 10.6 User-Facing Labels

Use **Profile** for the dropdown and filters.

Recommended UI labels:

| Old label | New label |
|---|---|
| Application Profile | Profile |
| Select application profile | Select profile |
| Type | Profile |
| Filter by type | Filter by profile |

Avoid showing `ApplicationProfile` as a UI label. That is an implementation term.

---

## 11. OpenAPI and Contract Tests

### 11.1 OpenAPI Schema

Rename schemas/properties:

```text
ApplicationProfile -> ApplicationProfile
type -> profile
query parameter type -> profile
```

Example schema:

```yaml
ApplicationProfile:
  type: string
  enum:
    - MachineToMachine
    - Web
    - SinglePage
    - Native
    - Device
    - Custom
```

Example request property:

```yaml
CreateApplicationRequest:
  type: object
  required:
    - clientId
    - displayName
    - profile
    - clientType
    - allowedGrantTypes
    - allowedScopes
    - redirectUris
    - postLogoutRedirectUris
    - requirePkce
    - requireConsent
  properties:
    profile:
      $ref: '#/components/schemas/ApplicationProfile'
```

Example list query parameter:

```yaml
- name: profile
  in: query
  required: false
  schema:
    $ref: '#/components/schemas/ApplicationProfile'
```

### 11.2 Contract Tests

Update contract tests to assert:

- create request accepts `profile`;
- responses contain `profile`;
- list filtering uses `?profile=...`;
- OpenAPI does not expose `ApplicationProfile` or `type` for the Applications API.

---

## 12. Documentation and Spec Kit Updates

Update these PR artifacts:

```text
docs/admin-applications.md
docs/applications-migration.md
specs/006-unify-applications-model/spec.md
specs/006-unify-applications-model/design.md
specs/006-unify-applications-model/data-model.md
specs/006-unify-applications-model/contracts/applications.openapi.yaml
specs/006-unify-applications-model/quickstart.md
specs/006-unify-applications-model/research.md
specs/006-unify-applications-model/tasks.md
README.md, if it mentions application profile
```

Recommended glossary entry:

```markdown
### Application

An administrator-managed OAuth 2.0 / OpenID Connect client application registration.

In OAuth 2.0 terminology, an Application represents a Client. In OpenID Connect user-authentication scenarios, it also acts as a Relying Party. Open Identity Stack uses the product-facing term Application.

### Application Profile

A product-level profile that determines the defaults, constraints, and available options for an Application, such as Web, Single Page, Native, Machine-to-machine, Device, or Custom.

The Application Profile is not the same as the OpenID Connect Dynamic Client Registration `application_type` metadata.
```

---

## 13. Search-and-Replace Checklist

Use this as the implementation checklist.

### 13.1 C# Domain/Application/API

- [ ] Rename `ApplicationProfile.cs` to `ApplicationProfile.cs`.
- [ ] Rename enum `ApplicationProfile` to `ApplicationProfile`.
- [ ] Rename `Application.Type` to `Application.Profile`.
- [ ] Rename constructor parameter `type` to `profile`.
- [ ] Rename `CreateApplicationCommand.Type` to `Profile`.
- [ ] Rename `ConfigureApplicationOAuthCommand.Type` to `Profile`.
- [ ] Rename `ApplicationDetails.Type` to `Profile`.
- [ ] Rename `ApplicationSummary.Type` to `Profile`.
- [ ] Rename `CreateApplicationRequest.Type` to `Profile`.
- [ ] Rename `ConfigureApplicationOAuthRequest.Type` to `Profile`.
- [ ] Rename `ApplicationCreatedResponse.Type` to `Profile`.
- [ ] Rename `ApplicationResponse.Type` to `Profile`.
- [ ] Rename `ApplicationListItemResponse.Type` to `Profile`.
- [ ] Rename `ListApplications` query parameter from `type` to `profile`.
- [ ] Rename repository/query filter parameter from `type` to `profile`.
- [ ] Keep `OAuthClientType` unchanged.
- [ ] Keep OpenIddict `ApplicationProfile` protocol property unchanged.

### 13.2 Infrastructure/Persistence

- [ ] Rename EF property mapping from `Type` to `Profile`.
- [ ] Rename DB column from `Type` to `Profile`.
- [ ] Rename index from `IX_Applications_Type` to `IX_Applications_Profile`.
- [ ] Update migration backfill SQL.
- [ ] Update migration preflight SQL.
- [ ] Update model snapshot.
- [ ] Update persistence tests.

### 13.3 Management Web

- [ ] Rename TypeScript `ApplicationProfile` constant/type to `ApplicationProfile`.
- [ ] Rename DTO fields from `type` to `profile`.
- [ ] Rename form field from `type` to `profile`.
- [ ] Rename dropdown label from `Application Profile` to `Profile`.
- [ ] Rename placeholder from `Select application profile` to `Select profile`.
- [ ] Update hooks and API mapping.
- [ ] Update list filters.
- [ ] Update tests and snapshots.

### 13.4 Specs/Docs/Contracts

- [ ] Rename OpenAPI schema `ApplicationProfile` to `ApplicationProfile`.
- [ ] Rename OpenAPI properties `type` to `profile`.
- [ ] Rename OpenAPI query parameter `type` to `profile`.
- [ ] Update contract test fixtures.
- [ ] Update admin docs.
- [ ] Update migration docs.
- [ ] Update Spec Kit feature artifacts.
- [ ] Update quickstart examples.

---

## 14. Acceptance Criteria

### AC1 — Domain terminology

Given the unified Applications domain model,  
when the code is inspected,  
then no domain type named `ApplicationProfile` exists,  
and the product profile enum is named `ApplicationProfile`.

### AC2 — Aggregate property

Given an `Application` aggregate,  
when its product profile is accessed,  
then the property is named `Profile`,  
not `Type`.

### AC3 — API create contract

Given an admin creates an application,  
when the request payload is sent to `POST /api/admin/applications`,  
then the payload uses `profile`,  
and the API response also returns `profile`.

### AC4 — API list filter

Given an admin lists applications,  
when they filter by product profile,  
then the query parameter is `profile`, for example:

```http
GET /api/admin/applications?profile=MachineToMachine
```

### AC5 — Management Web dropdown

Given an admin opens the create application form,  
then the product profile dropdown is labelled **Profile**,  
and the form field name is `profile`.

### AC6 — OpenIddict protocol mapping

Given an application is projected to OpenIddict,  
then `Application.Profile` is mapped to `OpenIddictApplicationDescriptor.ApplicationProfile`,  
and OpenIddict protocol naming remains unchanged.

### AC7 — OpenAPI contract

Given the generated OpenAPI document,  
then the Applications API exposes `ApplicationProfile` and `profile`,  
and does not expose `ApplicationProfile` or `type` for the application profile field.

### AC8 — Persistence

Given the unified Applications migration is generated,  
then the `Applications` table contains a `Profile` column,  
not a `Type` column.

### AC9 — Tests

Given the solution test suite is executed,  
then domain, application, API, contract, infrastructure, and Management Web tests pass with the new terminology.

---

## 15. Non-Functional Requirements

- The rename must be semantic only; application creation, validation, projection, credential handling, and migration behavior must remain unchanged.
- The resulting naming must clearly distinguish product profile from OAuth/OIDC protocol terminology.
- The API contract should be internally consistent and avoid exposing both `type` and `profile`.
- The UI should use concise product language: **Profile**.

---

## 16. Recommended PR Note

Use this text in the PR description or commit message:

```markdown
### Terminology addendum: Application Profile

This PR keeps `Application` as the product/domain name for an administrator-managed OAuth/OIDC client application registration, but renames `ApplicationProfile` to `ApplicationProfile`.

Reason: `ApplicationProfile` conflicts conceptually with OIDC/OpenIddict protocol terminology (`application_type` / `OpenIddictApplicationDescriptor.ApplicationProfile`). The Open Identity Stack value is a product profile such as Web, Single Page, Native, Machine-to-machine, Device, or Custom. It controls defaults, constraints, and available Admin UI options.

The public Applications API now uses `profile` instead of `type`, and Management Web labels the dropdown as `Profile`.
```

