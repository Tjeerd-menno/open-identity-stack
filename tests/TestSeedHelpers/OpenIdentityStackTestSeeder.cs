using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure;
using OpenIdentityStack.Infrastructure.Persistence;

using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
namespace OpenIdentityStack.Testing;

public sealed class OpenIdentityStackTestSeeder : IAsyncDisposable
{
    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static readonly SemaphoreSlim ScopeLock = new(1, 1);
    private static readonly HashSet<string> SeededConnections = new(StringComparer.Ordinal);
    private static readonly HashSet<string> SeededServiceProviders = new(StringComparer.Ordinal);
    private static readonly string[] BaseScopes =
    [
        "api",
        "ois.admin",
        "openid",
        "profile",
        "email",
        "isotopes:read",
        "isotopes:write",
        "exports:read",
        "exports:write",
        "audit:read"
    ];

    private readonly IServiceProvider _serviceProvider;
    private readonly bool _disposeServiceProvider;

    private OpenIdentityStackTestSeeder(IServiceProvider serviceProvider, bool disposeServiceProvider)
    {
        _serviceProvider = serviceProvider;
        _disposeServiceProvider = disposeServiceProvider;
    }

    public static async Task<OpenIdentityStackTestSeeder> CreateAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        // Explicitly specify the Testing environment so OpenIddict uses development
        // certificates. The seeder runs in-process and does not inherit child-process
        // environment variables set via Aspire's WithEnvironment().
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = "Testing" })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(connectionString, configuration, "Testing");

        ServiceProvider provider = services.BuildServiceProvider();
        OpenIdentityStackDbContext dbContext = provider.GetRequiredService<OpenIdentityStackDbContext>();
        ILogger<OpenIdentityStackTestSeeder>? logger = provider.GetService<ILogger<OpenIdentityStackTestSeeder>>();
        IOpenIddictScopeManager scopeManager = provider.GetRequiredService<IOpenIddictScopeManager>();

        await SeedLock.WaitAsync(cancellationToken);
        try
        {
            if (IsSqliteConnectionString(connectionString))
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }
            else
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
            }

            if (SeededConnections.Add(connectionString))
            {
                await SeedData.SeedAsync(dbContext, logger, cancellationToken);
            }

            await EnsureScopesAsync(scopeManager, BaseScopes, cancellationToken);
        }
        finally
        {
            SeedLock.Release();
        }

        return new OpenIdentityStackTestSeeder(provider, disposeServiceProvider: true);
    }

    private static bool IsSqliteConnectionString(string connectionString)
    {
        return connectionString.Contains("Data Source", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("DataSource", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<OpenIdentityStackTestSeeder> CreateAsync(
        IServiceProvider serviceProvider,
        string seedKey,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        OpenIdentityStackDbContext dbContext = scope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>();
        ILogger<OpenIdentityStackTestSeeder>? logger = scope.ServiceProvider.GetService<ILogger<OpenIdentityStackTestSeeder>>();
        IOpenIddictScopeManager scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        await SeedLock.WaitAsync(cancellationToken);
        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);

            if (SeededServiceProviders.Add(seedKey))
            {
                await SeedData.SeedAsync(dbContext, logger, cancellationToken);
            }

            await EnsureScopesAsync(scopeManager, BaseScopes, cancellationToken);
        }
        finally
        {
            SeedLock.Release();
        }

        return new OpenIdentityStackTestSeeder(serviceProvider, disposeServiceProvider: false);
    }

    public async Task CreateServiceAccountAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string>? allowedScopes = null,
        IReadOnlyList<string>? allowedGrantTypes = null,
        IReadOnlyList<string>? redirectUris = null,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IOpenIddictApplicationManager applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        IOpenIddictScopeManager scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        IApplicationRepository applicationRepository = scope.ServiceProvider.GetRequiredService<IApplicationRepository>();
        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        IDateTimeProvider dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        IReadOnlyList<string> resolvedScopes = allowedScopes is { Count: > 0 }
            ? allowedScopes
            : ["api"];

        IReadOnlyList<string> resolvedGrantTypes = allowedGrantTypes is { Count: > 0 }
            ? allowedGrantTypes
            : ["client_credentials"];

        await EnsureScopesAsync(scopeManager, resolvedScopes, cancellationToken);

        DomainApplication? existingApplication = await applicationRepository.GetByClientIdAsync(clientId, cancellationToken);
        if (existingApplication is null)
        {
            Result<DomainApplication> createApplicationResult = DomainApplication.Create(
                clientId,
                $"Test Service Account - {clientId}",
                null,
                ApplicationProfile.MachineToMachine,
                OAuthClientType.Confidential,
                resolvedGrantTypes,
                resolvedScopes,
                [],
                [],
                requirePkce: false,
                requireConsent: false,
                dateTimeProvider);

            if (createApplicationResult.IsSuccess)
            {
                DomainApplication application = createApplicationResult.Value;
                application.AddSecret(
                    passwordHasher.HashPassword(clientSecret),
                    "Initial test credential",
                    null,
                    dateTimeProvider);

                await applicationRepository.AddAsync(application, cancellationToken);
                await applicationRepository.SaveChangesAsync(cancellationToken);
            }
        }

        object? existingApp = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (existingApp is not null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = $"Test Service Account - {clientId}",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Introspection,
                OpenIddictConstants.Permissions.Endpoints.Revocation
            }
        };

        foreach (string grantType in resolvedGrantTypes)
        {
            if (string.Equals(grantType, "client_credentials", StringComparison.OrdinalIgnoreCase))
            {
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
            }
            if (string.Equals(grantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
            {
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            }
            if (string.Equals(grantType, "refresh_token", StringComparison.OrdinalIgnoreCase))
            {
                descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            }
        }

        if (redirectUris is { Count: > 0 })
        {
            foreach (string redirectUri in redirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(redirectUri));
            }
        }

        foreach (string scopeName in resolvedScopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scopeName);
        }

        await applicationManager.CreateAsync(descriptor, cancellationToken);
    }

    /// <summary>Bootstraps only an isolated test database; production approval handlers remain enabled.</summary>
    public async Task BootstrapHumanAdministratorFixtureAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        OpenIdentityStackDbContext db = scope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>();
        User user = await db.Users.SingleAsync(user => user.Id == new UserId(userId), cancellationToken);
        Role role = Role.Create($"fixture-administrator-{userId:N}", "Explicit isolated browser fixture authority").Value;
        role.AddPermission("*");
        db.Roles.Add(role);
        db.RoleAssignments.Add(RoleAssignment.Create(user.Id, role.Id, DateTimeOffset.UtcNow).Value);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task GrantAdministrativeFixtureAccessAsync(string clientId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OpenIdentityStack.Infrastructure.Resources.ResourceAccessBootstrapper>().InitializeAsync(cancellationToken);
        OpenIdentityStackDbContext db = scope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>();
        DomainApplication application = await db.Applications.SingleAsync(application => application.ClientId == clientId, cancellationToken);
        if (!await db.ClientResourceGrants.AnyAsync(grant => grant.ClientApplicationId == application.Id && grant.ResourceId == OpenIdentityStack.Domain.Resources.ProtectedResource.AdministrativeResourceId, cancellationToken))
        {
            db.ClientResourceGrants.Add(OpenIdentityStack.Domain.Resources.ClientResourceGrant.Create(application.Id,
                OpenIdentityStack.Domain.Resources.ProtectedResource.AdministrativeResourceId, ["*"], ["*"]).Value);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<Guid> CreateTestUserAsync(
        string email,
        string displayName,
        string password,
        UserProfileData? profile = null,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        ICreateUserUseCase createUserUseCase = scope.ServiceProvider.GetRequiredService<ICreateUserUseCase>();
        IUserRepository userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IDateTimeProvider dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        User? existingUser = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (existingUser is not null)
        {
            if (existingUser.Status == UserStatus.PendingVerification)
            {
                existingUser.VerifyEmail(dateTimeProvider);
                await userRepository.SaveChangesAsync(cancellationToken);
            }

            return existingUser.Id.Value;
        }

        var command = new CreateUserCommand(email, displayName, password, "test-seeder", profile);
        Result<CreateUserResult> result = await createUserUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Description);
        }

        User? user = await userRepository.GetByIdAsync(result.Value.UserId, cancellationToken);
        if (user is not null && user.Status == UserStatus.PendingVerification)
        {
            user.VerifyEmail(dateTimeProvider);
            await userRepository.SaveChangesAsync(cancellationToken);
        }

        return result.Value.UserId.Value;
    }

    public async Task DisableUserAsync(Guid userId, string reason = "Test disabled user", CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IUserRepository userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IDateTimeProvider dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        User? user = await userRepository.GetByIdAsync(new UserId(userId), cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            user.VerifyEmail(dateTimeProvider);
        }

        Result result = user.Disable(reason, dateTimeProvider);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Description);
        }

        await userRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task VerifyUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IUserRepository userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IDateTimeProvider dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        User? user = await userRepository.GetByIdAsync(new UserId(userId), cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            user.VerifyEmail(dateTimeProvider);
            await userRepository.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ValidateUserCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IValidateUserCredentialsUseCase validateUserCredentialsUseCase = scope.ServiceProvider.GetRequiredService<IValidateUserCredentialsUseCase>();
        IUserRepository userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        IDateTimeProvider dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        User? user = await userRepository.GetByEmailAsync(email, cancellationToken);
        if (user is not null && user.Status == UserStatus.PendingVerification)
        {
            user.VerifyEmail(dateTimeProvider);
            await userRepository.SaveChangesAsync(cancellationToken);
        }

        var command = new ValidateUserCredentialsCommand(email, password);
        Result<ValidateUserCredentialsResult> result = await validateUserCredentialsUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Description);
        }
    }

    public async Task<Guid> CreateTestRoleAsync(
        string name,
        string? description = null,
        IReadOnlyList<string>? permissions = null,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        ICreateRoleUseCase createRoleUseCase = scope.ServiceProvider.GetRequiredService<ICreateRoleUseCase>();
        IRoleRepository roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        Role? existingRole = await roleRepository.GetByNameAsync(name, cancellationToken);
        if (existingRole is not null)
        {
            return existingRole.Id.Value;
        }

        var command = new CreateRoleCommand(name, name, description, permissions);
        Result<CreateRoleResponse> result = await createRoleUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Description);
        }

        return result.Value.Id;
    }

    public async Task AssignRoleToUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IAssignRoleUseCase assignRoleUseCase = scope.ServiceProvider.GetRequiredService<IAssignRoleUseCase>();

        var command = new AssignRoleCommand(new UserId(userId), new RoleId(roleId), "test-seeder");
        Result result = await assignRoleUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Description);
        }
    }

    public async Task<IReadOnlyList<(Guid Id, string Name, string DisplayName)>> GetUserRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler =
            scope.ServiceProvider.GetRequiredService<IGetUserEffectiveRolesQueryHandler>();

        Result<IReadOnlyList<RoleDto>> result = await getUserEffectiveRolesQueryHandler
            .HandleAsync(new UserId(userId), cancellationToken);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Description);
        }

        return result.Value.Select(role => (role.Id, role.Name, role.DisplayName)).ToList();
    }

    public async Task<Guid> CreateSessionAsync(
        Guid userId,
        string? ipAddress = null,
        string? userAgent = null,
        int? durationMinutes = null,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        ICreateSessionUseCase createSessionUseCase = scope.ServiceProvider.GetRequiredService<ICreateSessionUseCase>();

        var command = new CreateSessionCommand(
            new UserId(userId),
            ipAddress ?? "127.0.0.1",
            userAgent ?? "TestAgent",
            durationMinutes.HasValue ? TimeSpan.FromMinutes(durationMinutes.Value) : null);

        Result<CreateSessionResult> result = await createSessionUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Description);
        }

        return result.Value.SessionId.Value;
    }

    /// <summary>
    /// Registers a confidential <c>client_credentials</c> client as a unified Application domain
    /// entity (with a hashed secret) plus its OpenIddict registration — but no <c>ServiceAccount</c>
    /// row (that table is not part of the migrated Postgres schema). The custom client-credentials
    /// authentication handler validates the secret against the Application entity, so this is
    /// sufficient to obtain a token at <c>/connect/token</c>; in the Testing environment a token
    /// requesting scope <c>api</c> is granted permission '*'.
    /// </summary>
    public async Task CreateConfidentialClientAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string>? allowedScopes = null,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IOpenIddictApplicationManager applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        IOpenIddictScopeManager scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        IApplicationRepository applicationRepository = scope.ServiceProvider.GetRequiredService<IApplicationRepository>();
        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        IDateTimeProvider dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        IReadOnlyList<string> resolvedScopes = allowedScopes is { Count: > 0 } ? allowedScopes : ["api"];
        IReadOnlyList<string> resolvedGrantTypes = ["client_credentials"];

        await EnsureScopesAsync(scopeManager, resolvedScopes, cancellationToken);

        DomainApplication? existingApplication = await applicationRepository.GetByClientIdAsync(clientId, cancellationToken);
        if (existingApplication is null)
        {
            Result<DomainApplication> createResult = DomainApplication.Create(
                clientId,
                $"Confidential Client - {clientId}",
                null,
                ApplicationProfile.MachineToMachine,
                OAuthClientType.Confidential,
                resolvedGrantTypes,
                resolvedScopes,
                [],
                [],
                requirePkce: false,
                requireConsent: false,
                dateTimeProvider);

            if (createResult.IsFailure)
            {
                throw new InvalidOperationException(createResult.Error.Description);
            }

            DomainApplication application = createResult.Value;
            application.AddSecret(passwordHasher.HashPassword(clientSecret), "Initial test credential", null, dateTimeProvider);
            await applicationRepository.AddAsync(application, cancellationToken);
            await applicationRepository.SaveChangesAsync(cancellationToken);
        }

        object? existingApp = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (existingApp is not null)
        {
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = $"Confidential Client - {clientId}",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.Introspection,
                OpenIddictConstants.Permissions.Endpoints.Revocation,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
            },
        };

        foreach (string scopeName in resolvedScopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scopeName);
        }

        await applicationManager.CreateAsync(descriptor, cancellationToken);
    }

    public async Task CreatePublicClientAsync(
        string clientId,
        string? displayName,
        IReadOnlyList<string> redirectUris,
        IReadOnlyList<string>? postLogoutRedirectUris = null,
        IReadOnlyList<string>? allowedScopes = null,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        IOpenIddictApplicationManager applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        IOpenIddictScopeManager scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        IReadOnlyList<string> resolvedScopes = allowedScopes is { Count: > 0 }
            ? allowedScopes
            : ["openid", "profile", "email", "api"];

        await EnsureScopesAsync(scopeManager, resolvedScopes, cancellationToken);

        object? existingApp = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (existingApp is not null)
        {
            var existingDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(existingDescriptor, existingApp, cancellationToken);

            existingDescriptor.RedirectUris.Clear();
            foreach (string redirectUri in redirectUris)
            {
                existingDescriptor.RedirectUris.Add(new Uri(redirectUri));
            }

            existingDescriptor.Permissions.Clear();
            existingDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            existingDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            existingDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
            existingDescriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            existingDescriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            existingDescriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);

            foreach (string scopeName in resolvedScopes)
            {
                existingDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scopeName);
            }

            if (postLogoutRedirectUris is { Count: > 0 })
            {
                existingDescriptor.PostLogoutRedirectUris.Clear();
                foreach (string postLogoutUri in postLogoutRedirectUris)
                {
                    existingDescriptor.PostLogoutRedirectUris.Add(new Uri(postLogoutUri));
                }
            }

            await applicationManager.UpdateAsync(existingApp, existingDescriptor, cancellationToken);
            return;
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = displayName ?? $"Public Client - {clientId}",
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
            },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange }
        };

        foreach (string scopeName in resolvedScopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scopeName);
        }

        foreach (string redirectUri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri));
        }

        if (postLogoutRedirectUris is { Count: > 0 })
        {
            foreach (string postLogoutUri in postLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(postLogoutUri));
            }
        }

        await applicationManager.CreateAsync(descriptor, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposeServiceProvider && _serviceProvider is IAsyncDisposable asyncDisposable)
        {
            return asyncDisposable.DisposeAsync();
        }

        if (_disposeServiceProvider && _serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static async Task EnsureScopesAsync(
        IOpenIddictScopeManager scopeManager,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken)
    {
        await ScopeLock.WaitAsync(cancellationToken);
        try
        {
            foreach (string scopeName in scopes.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (await scopeManager.FindByNameAsync(scopeName, cancellationToken).ConfigureAwait(false) is not null)
                {
                    continue;
                }

                try
                {
                    await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
                    {
                        Name = scopeName,
                        DisplayName = $"{scopeName} Scope"
                    }, cancellationToken);
                }
                catch (DbUpdateException ex) when (IsScopeUniqueViolation(ex))
                {
                    // Another concurrent seeding operation created the scope. Safe to ignore.
                }
            }
        }
        finally
        {
            ScopeLock.Release();
        }
    }

    private static bool IsScopeUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message.Contains("IX_OpenIddictScopes_Name", StringComparison.OrdinalIgnoreCase) == true
            || ex.Message.Contains("IX_OpenIddictScopes_Name", StringComparison.OrdinalIgnoreCase);
    }
}
