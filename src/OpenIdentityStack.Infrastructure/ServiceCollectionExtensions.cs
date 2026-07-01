using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Application.Federation.Queries;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Settings.Commands;
using OpenIdentityStack.Application.Settings.Queries;
using OpenIdentityStack.Infrastructure.Persistence.Groups;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.ApplicationPermissions;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Common;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Persistence.Roles;
using OpenIdentityStack.Infrastructure.Persistence.ApplicationPermissions;
using OpenIdentityStack.Infrastructure.Persistence.Sessions;
using OpenIdentityStack.Infrastructure.Persistence.Settings;
using OpenIdentityStack.Infrastructure.Persistence.Users;
using OpenIdentityStack.Infrastructure.ApplicationPermissions;
using OpenIdentityStack.Infrastructure.Applications;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        string environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Development;

        IConfiguration configuration = BuildDefaultConfiguration(environmentName);

        return services.AddInfrastructure(
            connectionString,
            configuration,
            environmentName);
    }

    private static IConfiguration BuildDefaultConfiguration(string environmentName)
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Adds Infrastructure services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration,
        string environmentName)
    {
        bool useSqliteForTesting =
            string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)
            && IsSqliteConnectionString(connectionString);

        services.AddDbContext<OpenIdentityStackDbContext>(options =>
            ConfigureDbContext(options, connectionString, useSqliteForTesting));

        AddCommonServices(services, configuration, environmentName);

        return services;
    }

    /// <summary>
    /// Adds Infrastructure services with Aspire integration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="connectionName">The Aspire connection name for the database.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructureWithAspire(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string connectionName = "openidentitystack")
    {
        string? connectionString = configuration.GetConnectionString(connectionName);

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{connectionName}' not found. Ensure the Aspire AppHost is configured correctly.");
        }

        bool useSqliteForTesting =
            environment.IsEnvironment("Testing")
            && IsSqliteConnectionString(connectionString);

        services.AddDbContext<OpenIdentityStackDbContext>(options =>
            ConfigureDbContext(options, connectionString, useSqliteForTesting));

        AddCommonServices(services, configuration, environment.EnvironmentName);

        return services;
    }

    private static void ConfigureDbContext(
        DbContextOptionsBuilder options,
        string connectionString,
        bool useSqliteForTesting)
    {
        if (useSqliteForTesting)
        {
            options.UseSqlite(connectionString);
        }
        else
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(OpenIdentityStackDbContext).Assembly.FullName);
            });
        }

        // Register OpenIddict entity sets
        options.UseOpenIddict();
    }

    private static bool IsSqliteConnectionString(string connectionString)
    {
        return connectionString.Contains("Data Source", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("DataSource", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddCommonServices(
        IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        // Register OpenIddict
        services.AddOpenIddictConfiguration(configuration, environmentName);

        AddPlatformServices(services);
        AddRepositories(services);
        AddInfrastructureDomainServices(services);
        AddInfrastructureUseCases(services);
    }

    private static void AddPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IEnvironmentProvider, EnvironmentProvider>();
        services.AddScoped<IAuditLog, AuditLogService>();
        services.AddSingleton<ISecretProtector, AesSecretProtector>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IPasswordPolicyValidator, PasswordPolicyValidator>();
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IApplicationPermissionRegistryRepository, ApplicationPermissionRegistryRepository>();
        services.AddScoped<IPermissionAssignmentStore, RolePermissionAssignmentStore>();
        services.AddScoped<IApplicationPermissionTransactionRunner, ApplicationPermissionTransactionRunner>();
        services.AddHttpClient<IRemotePermissionManifestFetcher, RemotePermissionManifestFetcher>()
            .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IPermissionDiagnosticsReader, PermissionDiagnosticsReader>();
        services.AddScoped<IUpstreamProviderRepository, UpstreamProviderRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IAuthenticationSettingsRepository, AuthenticationSettingsRepository>();
        services.AddScoped<IAuditEntryReader, AuditEntryReader>();
    }

    private static void AddInfrastructureDomainServices(IServiceCollection services)
    {
        services.AddScoped<IApplicationProtocolProjection, OpenIddictApplicationProjection>();
        services.AddScoped<IRolePermissionDependencyReader, RolePermissionDependencyReader>();

        // Register Single Logout (SLO) notification services
        services.AddHttpClient<ILogoutNotifier, BackChannelLogoutNotifier>();
        services.AddScoped<IFrontChannelLogoutService, FrontChannelLogoutService>();
    }

    private static void AddInfrastructureUseCases(IServiceCollection services)
    {
        // Authentication settings use cases (implemented in Infrastructure).
        services.AddScoped<IGetAuthenticationSettingsQueryHandler, GetAuthenticationSettingsQueryHandler>();
        services.AddScoped<ISetDefaultProviderUseCase, SetDefaultProviderUseCase>();
        services.AddScoped<ISetLocalFallbackUseCase, SetLocalFallbackUseCase>();
    }
}
