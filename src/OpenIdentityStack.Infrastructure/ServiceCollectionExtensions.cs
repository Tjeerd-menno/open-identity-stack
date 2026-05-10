using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Application.Clients.Commands;
using OpenIdentityStack.Application.Clients.Queries;
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
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Application.ServiceAccounts.Queries;
using OpenIdentityStack.Application.ServicePermissions.Commands;
using OpenIdentityStack.Application.ServicePermissions.Queries;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Clients;
using OpenIdentityStack.Infrastructure.Common;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Persistence.Roles;
using OpenIdentityStack.Infrastructure.Persistence.ServiceAccounts;
using OpenIdentityStack.Infrastructure.Persistence.ServicePermissions;
using OpenIdentityStack.Infrastructure.Persistence.Sessions;
using OpenIdentityStack.Infrastructure.Persistence.Settings;
using OpenIdentityStack.Infrastructure.Persistence.Users;

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
        // Register DbContext with PostgreSQL
        services.AddDbContext<OpenIdentityStackDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(OpenIdentityStackDbContext).Assembly.FullName);
            });

            // Register OpenIddict entity sets
            options.UseOpenIddict();
        });

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
        });

        AddCommonServices(services, configuration, environment.EnvironmentName);

        return services;
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

        // Register common services
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IEnvironmentProvider, EnvironmentProvider>();
        services.AddScoped<IAuditLog, AuditLogService>();
        services.AddScoped<IClientApplicationRegistrar, OpenIddictClientApplicationRegistrar>();
        services.AddSingleton<ISecretProtector, AesSecretProtector>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IPasswordPolicyValidator, PasswordPolicyValidator>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IServiceAccountRepository, ServiceAccountRepository>();
        services.AddScoped<IServicePermissionRegistryRepository, ServicePermissionRegistryRepository>();
        services.AddScoped<IUpstreamProviderRepository, UpstreamProviderRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();

        // Register use cases
        services.AddScoped<ICreateUserUseCase, CreateUserUseCase>();
        services.AddScoped<IValidateUserCredentialsUseCase, ValidateUserCredentialsUseCase>();
        services.AddScoped<IValidateClientCredentialsUseCase, ValidateClientCredentialsUseCase>();
        services.AddScoped<IValidateCertificateUseCase, ValidateCertificateUseCase>();
        services.AddScoped<IDisableUserUseCase, DisableUserUseCase>();
        services.AddScoped<IEnableUserUseCase, EnableUserUseCase>();
        services.AddScoped<IUpdateUserUseCase, UpdateUserUseCase>();
        services.AddScoped<IResetPasswordUseCase, ResetPasswordUseCase>();
        services.AddScoped<IDeleteUserUseCase, DeleteUserUseCase>();
        services.AddScoped<IGetUserQueryHandler, GetUserQueryHandler>();
        services.AddScoped<IListUsersQueryHandler, ListUsersQueryHandler>();

        // Register federation use cases
        services.AddScoped<IJitProvisionUserUseCase, JitProvisionUserUseCase>();
        services.AddScoped<ILinkUpstreamIdentityUseCase, LinkUpstreamIdentityUseCase>();
        services.AddScoped<IUnlinkUpstreamIdentityUseCase, UnlinkUpstreamIdentityUseCase>();
        services.AddScoped<IFindUserByUpstreamIdentityQueryHandler, FindUserByUpstreamIdentityQueryHandler>();
        services.AddScoped<IListUserUpstreamIdentitiesQueryHandler, ListUserUpstreamIdentitiesQueryHandler>();

        // Register role use cases
        services.AddScoped<ICreateRoleUseCase, CreateRoleUseCase>();
        services.AddScoped<IListRolesQueryHandler, ListRolesQueryHandler>();
        services.AddScoped<IAssignRoleUseCase, AssignRoleUseCase>();
        services.AddScoped<IUnassignRoleUseCase, UnassignRoleUseCase>();

        // Register group use cases
        services.AddScoped<ICreateGroupUseCase, CreateGroupUseCase>();
        services.AddScoped<IUpdateGroupUseCase, UpdateGroupUseCase>();
        services.AddScoped<IDeleteGroupUseCase, DeleteGroupUseCase>();
        services.AddScoped<IAddUserToGroupUseCase, AddUserToGroupUseCase>();
        services.AddScoped<IRemoveUserFromGroupUseCase, RemoveUserFromGroupUseCase>();
        services.AddScoped<IAddGroupMappingUseCase, AddGroupMappingUseCase>();
        services.AddScoped<IRemoveGroupMappingUseCase, RemoveGroupMappingUseCase>();
        services.AddScoped<IGroupRepository, GroupRepository>();

        // Register group query handlers
        services.AddScoped<IGetGroupQueryHandler, GetGroupQueryHandler>();
        services.AddScoped<IListGroupsQueryHandler, ListGroupsQueryHandler>();
        services.AddScoped<IListGroupMembersQueryHandler, ListGroupMembersQueryHandler>();
        services.AddScoped<IListGroupMappingsQueryHandler, ListGroupMappingsQueryHandler>();
        services.AddScoped<IGetUserGroupsQueryHandler, GetUserGroupsQueryHandler>();
        services.AddScoped<IGetGroupClaimsForUserQueryHandler, GetGroupClaimsForUserQueryHandler>();

        // Register role query handlers
        services.AddScoped<IGetUserRolesQueryHandler, GetUserRolesQueryHandler>();

        // Register user query handlers
        services.AddScoped<IGetUserEffectiveRolesQueryHandler, GetUserEffectiveRolesQueryHandler>();

        // Register service account use cases
        services.AddScoped<ICreateServiceAccountUseCase, CreateServiceAccountUseCase>();
        services.AddScoped<IUpdateServiceAccountUseCase, UpdateServiceAccountUseCase>();
        services.AddScoped<IDeleteServiceAccountUseCase, DeleteServiceAccountUseCase>();
        services.AddScoped<IListServiceAccountsQueryHandler, ListServiceAccountsQueryHandler>();
        services.AddScoped<IGetServiceAccountQueryHandler, GetServiceAccountQueryHandler>();
        services.AddScoped<IRotateSecretUseCase, RotateSecretUseCase>();
        services.AddScoped<IAddCertificateUseCase, AddCertificateUseCase>();
        services.AddScoped<IDisableServiceAccountUseCase, DisableServiceAccountUseCase>();
        services.AddScoped<IEnableServiceAccountUseCase, EnableServiceAccountUseCase>();

        // Register service permission registry use cases
        services.AddScoped<IRegisterServiceUseCase, RegisterServiceUseCase>();
        services.AddScoped<IListRegisteredServicesQueryHandler, ListRegisteredServicesQueryHandler>();
        services.AddScoped<IGetRegisteredServiceQueryHandler, GetRegisteredServiceQueryHandler>();
        services.AddScoped<IListAssignablePermissionCatalogQueryHandler, ListAssignablePermissionCatalogQueryHandler>();

        // Register session use cases
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ICreateSessionUseCase, CreateSessionUseCase>();
        services.AddScoped<IAddClientSessionUseCase, AddClientSessionUseCase>();
        services.AddScoped<IRevokeSessionUseCase, RevokeSessionUseCase>();
        services.AddScoped<IRevokeAllUserSessionsUseCase, RevokeAllUserSessionsUseCase>();
        services.AddScoped<IListSessionsQueryHandler, ListSessionsQueryHandler>();
        services.AddScoped<IValidateSessionQueryHandler, ValidateSessionQueryHandler>();

        // Register Single Logout (SLO) services
        services.AddScoped<IProcessLogoutUseCase, ProcessLogoutUseCase>();
        services.AddScoped<INotifyClientsOfLogoutUseCase, NotifyClientsOfLogoutUseCase>();
        services.AddHttpClient<ILogoutNotifier, BackChannelLogoutNotifier>();
        services.AddScoped<IFrontChannelLogoutService, FrontChannelLogoutService>();

        // Register provider management use cases
        services.AddScoped<ICreateProviderUseCase, CreateProviderUseCase>();
        services.AddScoped<IUpdateProviderUseCase, UpdateProviderUseCase>();
        services.AddScoped<IListProvidersQueryHandler, ListProvidersQueryHandler>();

        // Register client management use cases
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<ICreateClientUseCase, CreateClientUseCase>();
        services.AddScoped<IUpdateClientUseCase, UpdateClientUseCase>();
        services.AddScoped<IDeleteClientUseCase, DeleteClientUseCase>();
        services.AddScoped<IListClientsQueryHandler, ListClientsQueryHandler>();
        services.AddScoped<IGetClientQueryHandler, GetClientQueryHandler>();

        // Register authentication settings use cases
        services.AddScoped<IAuthenticationSettingsRepository, AuthenticationSettingsRepository>();
        services.AddScoped<IGetAuthenticationSettingsQueryHandler, GetAuthenticationSettingsQueryHandler>();
        services.AddScoped<ISetDefaultProviderUseCase, SetDefaultProviderUseCase>();
        services.AddScoped<ISetLocalFallbackUseCase, SetLocalFallbackUseCase>();
    }
}
