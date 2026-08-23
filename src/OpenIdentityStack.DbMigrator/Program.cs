using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
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
logger.LogInformation("Database migrations applied successfully.");

await SeedData.SeedAsync(dbContext, logger);
logger.LogInformation("Seeded base OpenIdentityStack data.");

bool seedCertificationProfile = IsCertificationSeedProfile(builder.Configuration);
bool seedDevData = builder.Environment.IsDevelopment()
    || builder.Environment.IsEnvironment("Testing")
    || builder.Configuration.GetValue<bool>("Seed:DevelopmentData");

await SeedManagementWebClientAsync(services);
if (seedCertificationProfile)
{
    await SeedCertificationDataAsync(services);
}

if (ShouldSeedDemoClients(builder.Configuration, builder.Environment, seedDevData, seedCertificationProfile))
{
    await SeedTraceableIsotopesWebClientAsync(services);
    await SeedIsotopesApiResourceClientAsync(services);
}

await SeedConfiguredAdminUserAsync(services);

if (seedDevData)
{
    logger.LogInformation("Seeding development/test data...");
    await SeedDefaultAdminUserAsync(services);
    logger.LogInformation("Development/test data seeding complete.");
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
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    IOpenIddictApplicationManager applicationManager = serviceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    IOpenIddictScopeManager scopeManager = serviceProvider.GetRequiredService<IOpenIddictScopeManager>();
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    IHostEnvironment hostEnvironment = serviceProvider.GetRequiredService<IHostEnvironment>();

    const string clientId = "management-web-client";

    string[] requiredScopes = ["openid", "profile", "email", "api"];
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
        DisplayName = "Management Web Application",
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
            OpenIddictConstants.Permissions.Prefixes.Scope + "api",
        },
        Requirements =
        {
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
        }
    };

    string[] configuredRedirectUris = GetConfiguredUris(configuration, "OpenIddict:Clients:ManagementWeb:RedirectUris");
    string[] configuredPostLogoutUris = GetConfiguredUris(configuration, "OpenIddict:Clients:ManagementWeb:PostLogoutRedirectUris");

    // Only register localhost redirect URIs in Development/Testing to avoid exposing them in production.
    bool isDevOrTesting = hostEnvironment.IsDevelopment() || hostEnvironment.IsEnvironment("Testing");
    if (isDevOrTesting)
    {
        string[] devRedirectUris =
        [
            "http://localhost:5175/auth/callback",
            "http://localhost:5175/auth/silent-callback",
            "http://localhost:5173/auth/callback",
            "http://localhost:5173/auth/silent-callback",
            "http://localhost:5174/auth/callback",
            "http://localhost:5174/auth/silent-callback",
            "http://localhost:3000/auth/callback",
            "http://localhost:3000/auth/silent-callback",
        ];

        string[] devPostLogoutUris =
        [
            "http://localhost:5175/",
            "http://localhost:5173/",
            "http://localhost:5174/",
            "http://localhost:3000/",
        ];

        foreach (string uri in devRedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        foreach (string uri in devPostLogoutUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }
    }

    foreach (string uri in configuredRedirectUris)
    {
        descriptor.RedirectUris.Add(new Uri(uri));
    }

    foreach (string uri in configuredPostLogoutUris)
    {
        descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
    }

    object? existingApp = await applicationManager.FindByClientIdAsync(clientId);
    if (existingApp is not null)
    {
        await applicationManager.UpdateAsync(existingApp, descriptor);
        logger.LogInformation("Updated OpenIddict public client '{ClientId}' for ManagementWeb", clientId);
        return;
    }

    await applicationManager.CreateAsync(descriptor);
    logger.LogInformation("Created OpenIddict public client '{ClientId}' for ManagementWeb", clientId);
}

static async Task SeedCertificationDataAsync(IServiceProvider serviceProvider)
{
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
    Uri issuer = new(GetRequiredConfiguration(configuration, "OpenIddict:Issuer"), UriKind.Absolute);

    logger.LogInformation("Seeding OpenID Foundation certification users and clients...");

    await UpsertCertificationUserAsync(
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
        GetRequiredConfiguration(configuration, "Seed:Certification:Users:Alice:Password"),
        resetPassword: configuration.GetValue("Seed:Certification:ResetExistingUsers", defaultValue: true));

    await UpsertCertificationUserAsync(
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
        GetRequiredConfiguration(configuration, "Seed:Certification:Users:Bob:Password"),
        resetPassword: configuration.GetValue("Seed:Certification:ResetExistingUsers", defaultValue: true));

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

    await SeedCertificationClientAsync(
        serviceProvider,
        "oidf-code-client",
        "OIDF Code Client",
        GetRequiredConfiguration(configuration, "Seed:Certification:Clients:CodeClientSecret"),
        redirectUris,
        scopes);

    await SeedCertificationClientAsync(
        serviceProvider,
        "oidf-code-client-post",
        "OIDF Code Client Post",
        GetRequiredConfiguration(configuration, "Seed:Certification:Clients:CodeClientPostSecret"),
        redirectUris,
        scopes);

    await SeedCertificationClientAsync(
        serviceProvider,
        "oidf-code-client-takeover",
        "OIDF Code Client Takeover",
        GetRequiredConfiguration(configuration, "Seed:Certification:Clients:CodeClientTakeoverSecret"),
        redirectUris,
        scopes);

    logger.LogInformation("OpenID Foundation certification seed profile completed.");
}

static async Task UpsertCertificationUserAsync(
    IServiceProvider serviceProvider,
    string email,
    string displayName,
    UserProfileData profile,
    string password,
    bool resetPassword)
{
    OpenIdentityStackDbContext db = serviceProvider.GetRequiredService<OpenIdentityStackDbContext>();
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    IPasswordHasher passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
    IPasswordPolicyValidator passwordPolicyValidator = serviceProvider.GetRequiredService<IPasswordPolicyValidator>();
    IDateTimeProvider dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();

    Result passwordValidation = passwordPolicyValidator.ValidatePassword(password);
    if (passwordValidation.IsFailure)
    {
        throw new InvalidOperationException($"Certification user password for '{email}' does not satisfy the password policy: {passwordValidation.Error.Description}");
    }

    string normalizedEmail = email.ToUpperInvariant();
    User? existingUser = await db.Users.FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail);
    if (existingUser is not null)
    {
        bool needsSave = false;

        if (existingUser.Status == UserStatus.PendingVerification)
        {
            Result verifyResult = existingUser.VerifyEmail(dateTimeProvider);
            if (verifyResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to verify certification user '{email}': {verifyResult.Error.Description}");
            }

            needsSave = true;
        }
        else if (existingUser.Status == UserStatus.Disabled)
        {
            Result enableResult = existingUser.Enable(dateTimeProvider);
            if (enableResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to enable certification user '{email}': {enableResult.Error.Description}");
            }

            needsSave = true;
        }

        if (!string.Equals(existingUser.DisplayName, displayName, StringComparison.Ordinal))
        {
            Result updateNameResult = existingUser.UpdateDisplayName(displayName, dateTimeProvider);
            if (updateNameResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to update certification user '{email}': {updateNameResult.Error.Description}");
            }

            needsSave = true;
        }

        if (existingUser.GetProfileData() != profile)
        {
            Result updateProfileResult = existingUser.UpdateProfile(profile, dateTimeProvider);
            if (updateProfileResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to update certification user profile for '{email}': {updateProfileResult.Error.Description}");
            }

            needsSave = true;
        }

        if (resetPassword || !existingUser.HasPassword())
        {
            Result setPasswordResult = existingUser.SetPassword(passwordHasher.HashPassword(password), dateTimeProvider);
            if (setPasswordResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to set certification user password for '{email}': {setPasswordResult.Error.Description}");
            }

            needsSave = true;
        }

        if (needsSave)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Updated certification user '{Email}'.", email);
        }

        return;
    }

    Result<User> userResult = User.CreateLocal(
        email,
        displayName,
        passwordHasher.HashPassword(password),
        dateTimeProvider,
        profile);
    if (userResult.IsFailure)
    {
        throw new InvalidOperationException($"Failed to create certification user '{email}': {userResult.Error.Description}");
    }

    User user = userResult.Value;
    Result activationResult = user.VerifyEmail(dateTimeProvider);
    if (activationResult.IsFailure)
    {
        throw new InvalidOperationException($"Failed to activate certification user '{email}': {activationResult.Error.Description}");
    }

    db.Users.Add(user);
    await db.SaveChangesAsync();
    logger.LogInformation("Created certification user '{Email}'.", email);
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
        await applicationManager.UpdateAsync(existingApplication, descriptor);
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
    OpenIdentityStackDbContext db = serviceProvider.GetRequiredService<OpenIdentityStackDbContext>();
    IPasswordHasher passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
    IPasswordPolicyValidator passwordPolicyValidator = serviceProvider.GetRequiredService<IPasswordPolicyValidator>();
    IDateTimeProvider dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();

    const string adminEmail = "admin@localhost.dev";
    const string adminDisplayName = "Default Admin";
    string? adminSecret = configuration["Seed:DefaultAdmin:Password"];

    if (string.IsNullOrWhiteSpace(adminSecret))
    {
        logger.LogInformation(
            "Skipping default admin user seeding. Configure Seed:DefaultAdmin:Password to enable the development admin account.");
        return;
    }

    Role? superAdminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "super-admin");

    User? existingAdmin = await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
    if (existingAdmin is not null)
    {
        bool needsSave = false;

        if (existingAdmin.Status != UserStatus.Active)
        {
            existingAdmin.VerifyEmail(dateTimeProvider);
            needsSave = true;
            logger.LogDebug("Activated existing admin user '{Email}'", adminEmail);
        }

        if (superAdminRole is not null)
        {
            bool hasRole = await db.RoleAssignments.AnyAsync(
                ra => ra.UserId == existingAdmin.Id && ra.RoleId == superAdminRole.Id);

            if (!hasRole)
            {
                Result<RoleAssignment> assignmentResult = RoleAssignment.Create(
                    existingAdmin.Id, superAdminRole.Id, dateTimeProvider.UtcNow);
                if (assignmentResult.IsSuccess)
                {
                    db.RoleAssignments.Add(assignmentResult.Value);
                    needsSave = true;
                    logger.LogInformation("Assigned super-admin role to existing admin user '{Email}'", adminEmail);
                }
            }
        }

        if (needsSave)
        {
            await db.SaveChangesAsync();
        }
        else
        {
            logger.LogDebug("Default admin user '{Email}' already exists and is properly configured", adminEmail);
        }
        return;
    }

    Result passwordValidation = passwordPolicyValidator.ValidatePassword(adminSecret);
    if (passwordValidation.IsFailure)
    {
        throw new InvalidOperationException($"Default admin password does not satisfy the password policy: {passwordValidation.Error.Description}");
    }

    string passwordHash = passwordHasher.HashPassword(adminSecret);
    Result<User> userResult = User.CreateLocal(adminEmail, adminDisplayName, passwordHash, dateTimeProvider);

    if (userResult.IsFailure)
    {
        logger.LogWarning("Failed to create default admin user: {Error}", userResult.Error.Description);
        return;
    }

    User adminUser = userResult.Value;
    adminUser.VerifyEmail(dateTimeProvider);

    db.Users.Add(adminUser);

    if (superAdminRole is not null)
    {
        Result<RoleAssignment> assignmentResult = RoleAssignment.Create(
            adminUser.Id, superAdminRole.Id, dateTimeProvider.UtcNow);
        if (assignmentResult.IsSuccess)
        {
            db.RoleAssignments.Add(assignmentResult.Value);
        }
    }

    await db.SaveChangesAsync();

    logger.LogInformation(
        "Created default admin user '{Email}' with super-admin role (Status: {Status}). Configure Seed:DefaultAdmin:Password to set a stable development password.",
        adminEmail,
        adminUser.Status);
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
        await applicationManager.UpdateAsync(existingApp, descriptor);
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
    if (existingClient is not null)
    {
        logger.LogDebug("OpenIddict client '{ClientId}' already exists, skipping seed", clientId);
        return;
    }

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

    await applicationManager.CreateAsync(descriptor);
    logger.LogInformation("Created OpenIddict introspection client '{ClientId}' for IsotopesApi", clientId);
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
    bool resetPasswordOnExistingUser = configuration.GetValue<bool>("Seed:AdminUser:ResetPasswordOnExistingUser");

    if (string.IsNullOrWhiteSpace(email))
    {
        throw new InvalidOperationException("Seed:AdminUser:Email must be configured when Seed:AdminUser:Enabled is true.");
    }

    if (string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException("Seed:AdminUser:Password must be configured when Seed:AdminUser:Enabled is true.");
    }

    await UpsertAdminUserAsync(serviceProvider, email, displayName, password, resetPasswordOnExistingUser);
}

static async Task UpsertAdminUserAsync(
    IServiceProvider serviceProvider,
    string adminEmail,
    string adminDisplayName,
    string adminPassword,
    bool resetPasswordOnExistingUser)
{
    ILogger<Program> logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    OpenIdentityStackDbContext db = serviceProvider.GetRequiredService<OpenIdentityStackDbContext>();
    IPasswordHasher passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher>();
    IPasswordPolicyValidator passwordPolicyValidator = serviceProvider.GetRequiredService<IPasswordPolicyValidator>();
    IDateTimeProvider dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
    string? passwordHash = null;
    string normalizedAdminEmail = adminEmail.ToUpperInvariant();
    Role? superAdminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "super-admin");
    User? existingAdmin = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedAdminEmail);

    if (existingAdmin is not null)
    {
        bool needsSave = false;

        if (existingAdmin.Status == UserStatus.PendingVerification)
        {
            Result verifyResult = existingAdmin.VerifyEmail(dateTimeProvider);
            if (verifyResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to verify configured admin user '{adminEmail}': {verifyResult.Error.Description}");
            }
            needsSave = true;
        }
        else if (existingAdmin.Status == UserStatus.Disabled)
        {
            Result enableResult = existingAdmin.Enable(dateTimeProvider);
            if (enableResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to enable configured admin user '{adminEmail}': {enableResult.Error.Description}");
            }
            needsSave = true;
        }

        if (!string.Equals(existingAdmin.DisplayName, adminDisplayName, StringComparison.Ordinal))
        {
            Result updateDisplayNameResult = existingAdmin.UpdateDisplayName(adminDisplayName, dateTimeProvider);
            if (updateDisplayNameResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to update configured admin user display name for '{adminEmail}': {updateDisplayNameResult.Error.Description}");
            }
            needsSave = true;
        }

        bool hadPassword = existingAdmin.HasPassword();
        if (!hadPassword || resetPasswordOnExistingUser)
        {
            Result setPasswordResult = existingAdmin.SetPassword(GetPasswordHash(), dateTimeProvider);
            if (setPasswordResult.IsFailure)
            {
                throw new InvalidOperationException($"Failed to set configured admin password for '{adminEmail}': {setPasswordResult.Error.Description}");
            }
            needsSave = true;
            logger.LogInformation(
                "Updated password for configured admin user '{Email}' ({Reason}).",
                adminEmail,
                hadPassword ? "reset requested" : "password was missing");
        }

        if (superAdminRole is not null)
        {
            bool hasRole = await db.RoleAssignments.AnyAsync(
                ra => ra.UserId == existingAdmin.Id && ra.RoleId == superAdminRole.Id);

            if (!hasRole)
            {
                Result<RoleAssignment> assignmentResult = RoleAssignment.Create(
                    existingAdmin.Id, superAdminRole.Id, dateTimeProvider.UtcNow);
                if (assignmentResult.IsFailure)
                {
                    throw new InvalidOperationException($"Failed to assign super-admin role to configured admin user '{adminEmail}': {assignmentResult.Error.Description}");
                }

                db.RoleAssignments.Add(assignmentResult.Value);
                needsSave = true;
            }
        }

        if (needsSave)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Upserted configured admin user '{Email}'.", adminEmail);
        }
        else
        {
            logger.LogInformation("Configured admin user '{Email}' already exists and is correctly configured.", adminEmail);
        }

        return;
    }

    string newUserPasswordHash = GetPasswordHash();
    Result<User> userResult = User.CreateLocal(adminEmail, adminDisplayName, newUserPasswordHash, dateTimeProvider);
    if (userResult.IsFailure)
    {
        throw new InvalidOperationException($"Failed to create configured admin user '{adminEmail}': {userResult.Error.Description}");
    }

    User adminUser = userResult.Value;
    Result activateResult = adminUser.VerifyEmail(dateTimeProvider);
    if (activateResult.IsFailure)
    {
        throw new InvalidOperationException($"Failed to activate configured admin user '{adminEmail}': {activateResult.Error.Description}");
    }

    db.Users.Add(adminUser);

    if (superAdminRole is not null)
    {
        Result<RoleAssignment> assignmentResult = RoleAssignment.Create(
            adminUser.Id, superAdminRole.Id, dateTimeProvider.UtcNow);
        if (assignmentResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to assign super-admin role to configured admin user '{adminEmail}': {assignmentResult.Error.Description}");
        }

        db.RoleAssignments.Add(assignmentResult.Value);
    }

    await db.SaveChangesAsync();
    logger.LogInformation("Created configured admin user '{Email}' with super-admin role.", adminEmail);

    string GetPasswordHash()
    {
        if (passwordHash is not null)
        {
            return passwordHash;
        }

        Result passwordValidation = passwordPolicyValidator.ValidatePassword(adminPassword);
        if (passwordValidation.IsFailure)
        {
            throw new InvalidOperationException($"Configured admin password does not satisfy the password policy: {passwordValidation.Error.Description}");
        }

        passwordHash = passwordHasher.HashPassword(adminPassword);
        return passwordHash;
    }
}

static string[] GetConfiguredUris(IConfiguration configuration, string key)
{
    string[]? values = configuration.GetSection(key).Get<string[]>();
    return values?.Where(static value => !string.IsNullOrWhiteSpace(value))
        .Select(static value => value.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        ?? [];
}
