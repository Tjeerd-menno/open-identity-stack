using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.DbMigrator;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure;
using OpenIdentityStack.Infrastructure.Common;
using OpenIdentityStack.Infrastructure.Persistence;

using SharedKernel;
#pragma warning disable CA1848 // Startup tool uses direct logging
#pragma warning disable CA1873 // Startup tool — log argument evaluation cost is negligible

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);


builder.Services.AddApplication();

string? connectionString = builder.Configuration.GetConnectionString("openidentitystack");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'openidentitystack' was not found.");
}

// Fail before any schema or seed work: the API refuses to start on a malformed key, and a
// migrator that completes anyway pushes the failure into a different workload.
SecretEncryptionKey.Validate(builder.Configuration);

builder.Services.AddInfrastructure(connectionString, builder.Configuration, builder.Environment.EnvironmentName);

using IHost host = builder.Build();
using IServiceScope scope = host.Services.CreateScope();
IServiceProvider services = scope.ServiceProvider;
ILogger<Program> logger = services.GetRequiredService<ILogger<Program>>();
OpenIdentityStackDbContext dbContext = services.GetRequiredService<OpenIdentityStackDbContext>();

logger.LogInformation("Applying OpenIdentityStack database migrations...");
await dbContext.Database.MigrateAsync();
await scope.ServiceProvider.GetRequiredService<OpenIdentityStack.Infrastructure.Resources.ResourceAccessBootstrapper>().InitializeAsync();
logger.LogInformation("Database migrations applied successfully.");

await SeedData.SeedAsync(dbContext, logger);
logger.LogInformation("Seeded base OpenIdentityStack data.");

bool seedCertificationProfile = IsCertificationSeedProfile(builder.Configuration);
bool seedDevData = builder.Environment.IsDevelopment()
    || builder.Environment.IsEnvironment("Testing")
    || builder.Configuration.GetValue<bool>("Seed:DevelopmentData");

await SeedManagementWebClientAsync(services);

if (ShouldSeedDemoClients(builder.Configuration, builder.Environment, seedDevData, seedCertificationProfile))
{
    await PrepareTraceableIsotopesWebClientAsync(services);
    await SeedIsotopesApiResourceClientAsync(services);
}

await SeedConfiguredAdminUserAsync(services);

if (seedDevData)
{
    logger.LogInformation("Seeding development/test data...");
    await SeedDefaultAdminUserAsync(services);
    logger.LogInformation("Development/test data seeding complete.");
}

if (seedCertificationProfile)
{
    await SeedCertificationDataAsync(services);
}


return;

static bool IsCertificationSeedProfile(IConfiguration configuration)
{
    return string.Equals(configuration["OPENIDENTITYSTACK_SEED_PROFILE"], "certification", StringComparison.OrdinalIgnoreCase)
        || string.Equals(configuration["Seed:Profile"], "certification", StringComparison.OrdinalIgnoreCase)
        || configuration.GetValue<bool>("Seed:Certification:Enabled");
}

static bool ShouldSeedDemoClients(
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    bool seedDevData,
    bool seedCertificationProfile)
{
    if (seedCertificationProfile)
    {
        return false;
    }

    // The demo clients carry a well-known, source-controlled client secret. Seeding them into a
    // production database would register a confidential client whose credentials are public, so
    // this is refused outright rather than left to configuration.
    if (hostEnvironment.IsProduction() || hostEnvironment.IsStaging())
    {
        return false;
    }

    bool? configured = configuration.GetValue<bool?>("Seed:DemoClients");
    if (configured.HasValue)
    {
        return configured.Value;
    }

    return hostEnvironment.IsDevelopment()
        || hostEnvironment.IsEnvironment("Testing")
        || seedDevData;
}

static async Task SeedManagementWebClientAsync(IServiceProvider serviceProvider)
{
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    IHostEnvironment environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    List<string> redirectUris = GetConfiguredUris(configuration, "OpenIddict:Clients:ManagementWeb:RedirectUris").ToList();
    List<string> postLogoutUris = GetConfiguredUris(configuration, "OpenIddict:Clients:ManagementWeb:PostLogoutRedirectUris").ToList();
    if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
    {
        foreach (int port in new[] { 5175, 5173, 5174, 3000 })
        {
            redirectUris.Add($"http://localhost:{port}/auth/callback");
            redirectUris.Add($"http://localhost:{port}/auth/silent-callback");
            postLogoutUris.Add($"http://localhost:{port}/");
        }
    }
    Result prepared = await serviceProvider.GetRequiredService<OpenIdentityStack.Application.AdministrativeAccess.ManagementWebPreparation>()
        .PrepareAsync(redirectUris.Distinct(StringComparer.Ordinal).ToArray(), postLogoutUris.Distinct(StringComparer.Ordinal).ToArray(),
            configuration.GetValue<bool>("Seed:AdministrativeAccess:BootstrapManagementWeb"));
    if (prepared.IsFailure) { throw new InvalidOperationException(prepared.Error.Description); }
}
static async Task SeedCertificationDataAsync(IServiceProvider serviceProvider)
{
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    Uri issuer = new(GetRequiredConfiguration(configuration, "OpenIddict:Issuer"), UriKind.Absolute);

    logger.LogInformation("Seeding OpenID Foundation certification users and clients...");

    await SeedCertificationUserAsync(
        serviceProvider,
        "alice@example.test",
        "Alice Certification",
        CreateCertificationUserProfile(
            issuer,
            "alice",
            givenName: "Alice",
            familyName: "Certification",
            middleName: "Marie",
            nickname: "ally",
            gender: "female",
            birthdate: "1990-01-15",
            zoneInfo: "Europe/Amsterdam",
            locale: "en-NL",
            address: new Address(
                Formatted: "Keizersgracht 1\n1015 CJ Amsterdam\nNetherlands",
                StreetAddress: "Keizersgracht 1",
                Locality: "Amsterdam",
                Region: "Noord-Holland",
                PostalCode: "1015 CJ",
                Country: "Netherlands"),
            phoneNumber: "+31 20 555 0100"),
        GetRequiredConfiguration(configuration, "Seed:Certification:Users:Alice:Password"));

    await SeedCertificationUserAsync(
        serviceProvider,
        "bob@example.test",
        "Bob Certification",
        CreateCertificationUserProfile(
            issuer,
            "bob",
            givenName: "Bob",
            familyName: "Certification",
            middleName: "Jan",
            nickname: "bobby",
            gender: "male",
            birthdate: "1988-07-04",
            zoneInfo: "Europe/Amsterdam",
            locale: "en-NL",
            address: new Address(
                Formatted: "Herengracht 42\n1015 BS Amsterdam\nNetherlands",
                StreetAddress: "Herengracht 42",
                Locality: "Amsterdam",
                Region: "Noord-Holland",
                PostalCode: "1015 BS",
                Country: "Netherlands"),
            phoneNumber: "+31 20 555 0101"),
        GetRequiredConfiguration(configuration, "Seed:Certification:Users:Bob:Password"));

    string[] redirectUris = GetCertificationRedirectUris(configuration);
    IReadOnlyList<string> scopes =
    [
        OpenIddictConstants.Scopes.OpenId,
        OpenIddictConstants.Scopes.Profile,
        OpenIddictConstants.Scopes.Email,
        OpenIddictConstants.Scopes.Address,
        OpenIddictConstants.Scopes.Phone,
        OpenIddictConstants.Scopes.OfflineAccess
    ];

    await PrepareCertificationClientAsync(
        serviceProvider,
        "oidf-code-client",
        "OIDF Code Client",
        GetRequiredConfiguration(configuration, "Seed:Certification:Clients:CodeClientSecret"),
        redirectUris,
        scopes);

    await PrepareCertificationClientAsync(
        serviceProvider,
        "oidf-code-client-post",
        "OIDF Code Client Post",
        GetRequiredConfiguration(configuration, "Seed:Certification:Clients:CodeClientPostSecret"),
        redirectUris,
        scopes);

    await PrepareCertificationClientAsync(
        serviceProvider,
        "oidf-code-client-takeover",
        "OIDF Code Client Takeover",
        GetRequiredConfiguration(configuration, "Seed:Certification:Clients:CodeClientTakeoverSecret"),
        redirectUris,
        scopes);

    logger.LogInformation("OpenID Foundation certification seed profile completed.");
}

static async Task SeedCertificationUserAsync(
    IServiceProvider serviceProvider,
    string email,
    string displayName,
    UserProfileData profile,
    string password)
{
    bool created = await serviceProvider.GetRequiredService<LocalUserBootstrapper>()
        .CreateIfAbsentAsync(email, displayName, password, assignAdministrator: false, profile);
    serviceProvider.GetRequiredService<ILogger<Program>>().LogInformation(
        "Certification bootstrap completed (Created: {Created}); existing accounts are preserved.", created);
}
static UserProfileData CreateCertificationUserProfile(
    Uri issuer,
    string preferredUsername,
    string givenName,
    string familyName,
    string middleName,
    string nickname,
    string gender,
    string birthdate,
    string zoneInfo,
    string locale,
    Address address,
    string phoneNumber)
{
    string profilePath = $"profiles/{preferredUsername}";
    string avatarPath = $"{profilePath}/avatar.svg";

    return new UserProfileData(
        GivenName: givenName,
        FamilyName: familyName,
        MiddleName: middleName,
        Nickname: nickname,
        PreferredUsername: preferredUsername,
        Profile: new Uri(issuer, profilePath).AbsoluteUri,
        Picture: new Uri(issuer, avatarPath).AbsoluteUri,
        Website: new Uri(issuer, profilePath).AbsoluteUri,
        Gender: gender,
        Birthdate: birthdate,
        ZoneInfo: zoneInfo,
        Locale: locale,
        Address: address,
        PhoneNumber: phoneNumber,
        // Nothing has verified these numbers; the suite checks the type, not the value.
        PhoneNumberVerified: false);
}

static async Task PrepareCertificationClientAsync(
    IServiceProvider serviceProvider,
    string clientId,
    string displayName,
    string clientSecret,
    IReadOnlyList<string> redirectUris,
    IReadOnlyList<string> scopes)
{
    var configuration = new SeededOAuthClientConfiguration(
        clientId, displayName, OpenIdentityStack.Domain.Applications.ApplicationProfile.Web,
        OpenIdentityStack.Domain.Applications.OAuthClientType.Confidential,
        [OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken],
        scopes, redirectUris, [], RequirePkce: false, RequireConsent: false);
    Result<OpenIdentityStack.Domain.Applications.Application> prepared = await serviceProvider
        .GetRequiredService<SeededOAuthClientPreparation>().PrepareAsync(configuration, clientSecret);
    if (prepared.IsFailure) { throw new InvalidOperationException(prepared.Error.Description); }
    await SeedCertificationClientAsync(serviceProvider, clientId, displayName, clientSecret, redirectUris, scopes);
    serviceProvider.GetRequiredService<ILogger<Program>>()
        .LogInformation("Prepared certification OAuth client '{ClientId}'.", clientId);
}

static async Task SeedCertificationClientAsync(
    IServiceProvider serviceProvider,
    string clientId,
    string displayName,
    string clientSecret,
    IReadOnlyList<string> redirectUris,
    IReadOnlyList<string> scopes)
{
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    IOpenIddictApplicationManager applicationManager = serviceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    IOpenIddictScopeManager scopeManager = serviceProvider.GetRequiredService<IOpenIddictScopeManager>();

    foreach (string scopeName in scopes)
    {
        if (await scopeManager.FindByNameAsync(scopeName) is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = scopeName,
                DisplayName = $"{scopeName} Scope"
            });
        }
    }

    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = clientId,
        ClientSecret = clientSecret,
        ClientType = OpenIddictConstants.ClientTypes.Confidential,
        ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
        DisplayName = displayName,
        Permissions =
        {
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.Endpoints.EndSession,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
        }
    };

    foreach (string redirectUri in redirectUris)
    {
        descriptor.RedirectUris.Add(new Uri(redirectUri, UriKind.Absolute));
    }

    foreach (string scopeName in scopes)
    {
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scopeName);
    }

    object? existingApplication = await applicationManager.FindByClientIdAsync(clientId);
    if (existingApplication is not null)
    {
        await SeededOpenIddictApplicationUpdater.UpdateAsync(applicationManager, existingApplication, descriptor);
        logger.LogInformation("Updated certification OpenIddict client '{ClientId}'.", clientId);
        return;
    }

    await applicationManager.CreateAsync(descriptor);
    logger.LogInformation("Created certification OpenIddict client '{ClientId}'.", clientId);
}

static string[] GetCertificationRedirectUris(IConfiguration configuration)
{
    string[] configuredRedirectUris = GetConfiguredUris(configuration, "Seed:Certification:RedirectUris");
    if (configuredRedirectUris.Length > 0)
    {
        return configuredRedirectUris;
    }

    string alias = GetRequiredConfiguration(configuration, "Seed:Certification:Alias");
    List<string> redirectUris =
    [
        $"https://www.certification.openid.net/test/a/{alias}/callback"
    ];

    if (configuration.GetValue("Seed:Certification:IncludeStagingRedirectUri", defaultValue: true))
    {
        redirectUris.Add($"https://staging.certification.openid.net/test/a/{alias}/callback");
    }

    return redirectUris.ToArray();
}

static string GetRequiredConfiguration(IConfiguration configuration, string key)
{
    string? value = configuration[key]?.Trim();
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Configuration value '{key}' is required for the certification seed profile.");
    }

    return value;
}

static async Task SeedDefaultAdminUserAsync(IServiceProvider serviceProvider)
{
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    string? password = configuration["Seed:DefaultAdmin:Password"];
    if (string.IsNullOrWhiteSpace(password))
    {
        logger.LogInformation("Skipping development admin bootstrap; no password is configured.");
        return;
    }

    bool created = await serviceProvider.GetRequiredService<LocalUserBootstrapper>()
        .CreateIfAbsentAsync("admin@localhost.dev", "Default Admin", password, assignAdministrator: true);
    logger.LogInformation("Development admin bootstrap completed (Created: {Created}); existing accounts are preserved.", created);
}
static async Task PrepareTraceableIsotopesWebClientAsync(IServiceProvider serviceProvider)
{
    SeededPermissionConfiguration[] permissions =
    [
        new("isotopes:read", "Read isotopes", null, "Isotopes"),
        new("isotopes:write", "Write isotopes", null, "Isotopes"),
        new("exports:read", "Read exports", null, "Exports"),
        new("exports:write", "Write exports", null, "Exports"),
        new("audit:read", "Read audit records", null, "Audit")
    ];
    string[] resourceScopes = permissions.Select(static permission => permission.PermissionKey).ToArray();
    string[] scopes =
    [
        OpenIddictConstants.Scopes.OpenId,
        OpenIddictConstants.Scopes.Profile,
        OpenIddictConstants.Scopes.Email,
        .. resourceScopes
    ];
    string[] redirectUris =
    [
        "http://localhost:5176/callback",
        "http://localhost:5173/callback",
        "http://localhost:5174/callback",
        "http://localhost:3000/callback",
    ];
    string[] postLogoutRedirectUris =
    [
        "http://localhost:5176/", "http://localhost:5176",
        "http://localhost:5173/", "http://localhost:5173",
        "http://localhost:5174/", "http://localhost:5174",
        "http://localhost:3000/", "http://localhost:3000",
    ];
    SeededProtectedResourceConfiguration[] resources = resourceScopes
        .Select(static scope => new SeededProtectedResourceConfiguration(
            $"urn:traceable-isotopes:scope:{scope}", scope,
            $"Traceable Isotopes {scope} access", ["traceable-isotopes"], [$"traceable-isotopes:{scope}"]))
        .ToArray();
    var catalog = new SeededPermissionCatalogConfiguration(
        "traceable-isotopes", "Traceable Isotopes", "deployment-seed",
        OpenIdentityStack.Domain.ApplicationPermissions.OwnerType.User, permissions);
    var configuration = new SeededOAuthClientConfiguration(
        "traceable-isotopes-web", "Traceable Isotopes Web Application",
        OpenIdentityStack.Domain.Applications.ApplicationProfile.SinglePage, OpenIdentityStack.Domain.Applications.OAuthClientType.Public,
        [OpenIddictConstants.GrantTypes.AuthorizationCode, OpenIddictConstants.GrantTypes.RefreshToken],
        scopes, redirectUris, postLogoutRedirectUris, RequirePkce: true, RequireConsent: false);
    Result<OpenIdentityStack.Domain.Applications.Application> prepared = await serviceProvider
        .GetRequiredService<SeededOAuthClientPreparation>().PrepareAsync(configuration, null, resources, catalog);
    if (prepared.IsFailure) { throw new InvalidOperationException(prepared.Error.Description); }
    await SeedTraceableIsotopesWebClientAsync(serviceProvider);
    serviceProvider.GetRequiredService<ILogger<Program>>()
        .LogInformation("Prepared Traceable Isotopes OAuth client and resource access.");
}

static async Task SeedTraceableIsotopesWebClientAsync(IServiceProvider serviceProvider)
{
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    IOpenIddictApplicationManager applicationManager = serviceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    IOpenIddictScopeManager scopeManager = serviceProvider.GetRequiredService<IOpenIddictScopeManager>();

    const string clientId = "traceable-isotopes-web";

    string[] requiredScopes =
    [
        "openid",
        "profile",
        "email",
        "isotopes:read",
        "isotopes:write",
        "exports:read",
        "exports:write",
        "audit:read",
    ];
    foreach (string scopeName in requiredScopes)
    {
        if (await scopeManager.FindByNameAsync(scopeName) is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = scopeName,
                DisplayName = $"{scopeName} Scope"
            });
            logger.LogDebug("Created OpenIddict scope '{ScopeName}'", scopeName);
        }
    }

    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = clientId,
        DisplayName = "Traceable Isotopes Web Application",
        ClientType = OpenIddictConstants.ClientTypes.Public,
        ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
        Permissions =
        {
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.Endpoints.EndSession,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
            OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
            OpenIddictConstants.Permissions.Prefixes.Scope + "email",
            OpenIddictConstants.Permissions.Prefixes.Scope + "isotopes:read",
            OpenIddictConstants.Permissions.Prefixes.Scope + "isotopes:write",
            OpenIddictConstants.Permissions.Prefixes.Scope + "exports:read",
            OpenIddictConstants.Permissions.Prefixes.Scope + "exports:write",
            OpenIddictConstants.Permissions.Prefixes.Scope + "audit:read",
        },
        Requirements =
        {
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
        }
    };

    string[] devRedirectUris =
    [
        "http://localhost:5176/callback",
        "http://localhost:5173/callback",
        "http://localhost:5174/callback",
        "http://localhost:3000/callback",
    ];

    string[] devPostLogoutUris =
    [
        "http://localhost:5176/",
        "http://localhost:5176",
        "http://localhost:5173/",
        "http://localhost:5173",
        "http://localhost:5174/",
        "http://localhost:5174",
        "http://localhost:3000/",
        "http://localhost:3000",
    ];

    foreach (string uri in devRedirectUris)
    {
        descriptor.RedirectUris.Add(new Uri(uri));
    }

    foreach (string uri in devPostLogoutUris)
    {
        descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
    }

    object? existingApp = await applicationManager.FindByClientIdAsync(clientId);
    if (existingApp is not null)
    {
        await SeededOpenIddictApplicationUpdater.UpdateAsync(applicationManager, existingApp, descriptor);
        logger.LogInformation("Updated OpenIddict public client '{ClientId}' for Traceable Isotopes Web", clientId);
        return;
    }

    await applicationManager.CreateAsync(descriptor);
    logger.LogInformation("Created OpenIddict public client '{ClientId}' for Traceable Isotopes Web", clientId);
}

static async Task SeedIsotopesApiResourceClientAsync(IServiceProvider serviceProvider)
{
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    IOpenIddictApplicationManager applicationManager = serviceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    IOpenIddictScopeManager scopeManager = serviceProvider.GetRequiredService<IOpenIddictScopeManager>();

    const string clientId = "isotopes-api-resource";

    // Well-known secret for a local demo client. ShouldSeedDemoClients refuses to run outside
    // development/testing, so this never reaches a production or staging database.
    const string clientSecret = "isotopes-api-resource-secret";

    string[] resourceScopes = ["isotopes:read", "isotopes:write", "exports:read", "exports:write", "audit:read"];
    SeededProtectedResourceConfiguration[] resources = resourceScopes.Select(static scope =>
        new SeededProtectedResourceConfiguration(
            $"urn:traceable-isotopes:scope:{scope}", scope,
            $"Traceable Isotopes {scope} access", ["traceable-isotopes"], [])).ToArray();
    var authorityConfiguration = new SeededOAuthClientConfiguration(
        clientId, "Isotopes API Resource Server",
        OpenIdentityStack.Domain.Applications.ApplicationProfile.MachineToMachine,
        OpenIdentityStack.Domain.Applications.OAuthClientType.Confidential,
        [OpenIddictConstants.GrantTypes.ClientCredentials], resourceScopes, [], [],
        RequirePkce: false, RequireConsent: false);
    Result<OpenIdentityStack.Domain.Applications.Application> prepared = await serviceProvider
        .GetRequiredService<SeededOAuthClientPreparation>()
        .PrepareAuthorityOnlyAsync(authorityConfiguration, resources);
    if (prepared.IsFailure) { throw new InvalidOperationException(prepared.Error.Description); }

    object? apiScope = await scopeManager.FindByNameAsync("api");
    if (apiScope is null)
    {
        logger.LogInformation("Creating 'api' scope with resource '{Resource}'", clientId);
        await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = "api",
            DisplayName = "API Access",
            Resources = { clientId }
        });
    }
    else
    {
        OpenIddictScopeDescriptor scopeDescriptor = new();
        await scopeManager.PopulateAsync(scopeDescriptor, apiScope);
        bool added = scopeDescriptor.Resources.Add(clientId);
        if (added)
        {
            logger.LogInformation("Updating 'api' scope to add resource '{Resource}'", clientId);
            await scopeManager.UpdateAsync(apiScope, scopeDescriptor);
        }
        else
        {
            logger.LogDebug("'api' scope already has resource '{Resource}'", clientId);
        }
    }

    object? existingClient = await applicationManager.FindByClientIdAsync(clientId);
    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = clientId,
        ClientSecret = clientSecret,
        DisplayName = "Isotopes API Resource Server",
        ClientType = OpenIddictConstants.ClientTypes.Confidential,
        Permissions =
        {
            OpenIddictConstants.Permissions.Endpoints.Introspection
        }
    };

    if (existingClient is null)
    {
        await applicationManager.CreateAsync(descriptor);
        logger.LogInformation("Created OpenIddict introspection client '{ClientId}' for IsotopesApi", clientId);
        return;
    }

    await SeededOpenIddictApplicationUpdater.UpdateAsync(applicationManager, existingClient, descriptor);
    logger.LogInformation("Updated OpenIddict introspection client '{ClientId}' for IsotopesApi", clientId);
}

static async Task SeedConfiguredAdminUserAsync(IServiceProvider serviceProvider)
{
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    if (!configuration.GetValue<bool>("Seed:AdminUser:Enabled"))
    {
        logger.LogDebug("Production admin user seeding is disabled.");
        return;
    }

    string? email = configuration["Seed:AdminUser:Email"]?.Trim();
    string? password = configuration["Seed:AdminUser:Password"];
    string displayName = configuration["Seed:AdminUser:DisplayName"]?.Trim() switch
    {
        { Length: > 0 } configuredDisplayName => configuredDisplayName,
        _ => "Production Admin"
    };

    if (string.IsNullOrWhiteSpace(email))
    {
        throw new InvalidOperationException("Seed:AdminUser:Email must be configured when Seed:AdminUser:Enabled is true.");
    }

    if (string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException("Seed:AdminUser:Password must be configured when Seed:AdminUser:Enabled is true.");
    }

    await CreateConfiguredAdminUserAsync(serviceProvider, email, displayName, password);
}

static async Task CreateConfiguredAdminUserAsync(
    IServiceProvider serviceProvider,
    string email,
    string displayName,
    string password)
{
    bool created = await serviceProvider.GetRequiredService<LocalUserBootstrapper>()
        .CreateIfAbsentAsync(email, displayName, password, assignAdministrator: true);
    serviceProvider.GetRequiredService<ILogger<Program>>().LogInformation(
        "Configured admin bootstrap completed (Created: {Created}); existing accounts are preserved.", created);
}
static string[] GetConfiguredUris(IConfiguration configuration, string key)
{
    string[]? values = configuration.GetSection(key).Get<string[]>();
    return values?.Where(static value => !string.IsNullOrWhiteSpace(value))
        .Select(static value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        ?? [];
}
