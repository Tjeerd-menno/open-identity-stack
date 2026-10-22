using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions;
using OpenIdentityStack.Application.ApplicationPermissions.Commands;
using OpenIdentityStack.Application.ApplicationPermissions.Queries;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Application.Audit.Queries;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Application.Federation.Queries;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Sessions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Application.Users.Queries;

namespace OpenIdentityStack.Application;

/// <summary>
/// Registers Application-layer use cases, query handlers, and domain services
/// (everything whose implementation lives in the Application assembly).
/// Infrastructure wires the abstractions these depend on (repositories, projections, etc.).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        AddUserUseCases(services);
        AddFederationUseCases(services);
        AddRoleUseCases(services);
        AddGroupUseCases(services);
        AddApplicationUseCases(services);
        AddApplicationPermissionUseCases(services);
        AddSessionAndLogoutUseCases(services);
        AddAuditUseCases(services);
        AddProviderUseCases(services);
        AddAuthorizationServices(services);
        services.AddScopedInternal<IAdministrativeApproval, AdministrativeApproval>();
        services.AddScopedInternal<AdministrativeAccess.AdministrativeAccessWorkflow>();
        services.AddScopedInternal<AdministrativeAccess.ManagementWebPreparation>();
        services.AddScoped<IAdministrativeClientGuard>(provider => provider.GetRequiredService<AdministrativeAccess.AdministrativeAccessWorkflow>());
        services.AddScoped<IAdministrativeAccessEvaluator, AdministrativeAccess.AdministrativeAccessEvaluator>();
        services.TryAddSingleton<IAdministrativeActorContext, UnauthenticatedAdministrativeActorContext>();
        services.AddScopedInternal<UnrestrictedGrantPolicy>();
        services.AddScopedInternal<Applications.SeededOAuthClientPreparation>();

        return services;
    }

    private static void AddUserUseCases(IServiceCollection services)
    {
        services.AddScopedInternal<ICreateUserUseCase, CreateUserUseCase>();
        services.AddScopedInternal<IValidateUserCredentialsUseCase, ValidateUserCredentialsUseCase>();
        services.AddScopedInternal<IDisableUserUseCase, DisableUserUseCase>();
        services.AddScopedInternal<IEnableUserUseCase, EnableUserUseCase>();
        services.AddScopedInternal<IUpdateUserUseCase, UpdateUserUseCase>();
        services.AddScopedInternal<IResetPasswordUseCase, ResetPasswordUseCase>();
        services.AddScopedInternal<IDeleteUserUseCase, DeleteUserUseCase>();
        services.AddScopedInternal<IGetUserQueryHandler, GetUserQueryHandler>();
        services.AddScopedInternal<IGetPublicProfileQueryHandler, GetPublicProfileQueryHandler>();
        services.AddScopedInternal<IListUsersQueryHandler, ListUsersQueryHandler>();
        services.AddScopedInternal<IGetUserEffectiveRolesQueryHandler, GetUserEffectiveRolesQueryHandler>();
    }

    private static void AddFederationUseCases(IServiceCollection services)
    {
        services.AddScopedInternal<IJitProvisionUserUseCase, JitProvisionUserUseCase>();
        services.AddScoped<SetProviderEmailVerificationTrust>();
        services.AddScoped<ILinkUpstreamIdentityUseCase, LinkUpstreamIdentityUseCase>();
        services.AddScopedInternal<IUnlinkUpstreamIdentityUseCase, UnlinkUpstreamIdentityUseCase>();
        services.AddScopedInternal<IFindUserByUpstreamIdentityQueryHandler, FindUserByUpstreamIdentityQueryHandler>();
        services.AddScopedInternal<IListUserUpstreamIdentitiesQueryHandler, ListUserUpstreamIdentitiesQueryHandler>();
        services.AddScopedInternal<IIdentityMigrationInventoryQueryHandler, IdentityMigrationInventoryQueryHandler>();
    }

    private static void AddRoleUseCases(IServiceCollection services)
    {
        services.AddScopedInternal<ICreateRoleUseCase, CreateRoleUseCase>();
        services.AddScopedInternal<IUpdateRoleUseCase, UpdateRoleUseCase>();
        services.AddScopedInternal<IDeleteRoleUseCase, DeleteRoleUseCase>();
        services.AddScopedInternal<IDisableRoleUseCase, DisableRoleUseCase>();
        services.AddScopedInternal<IEnableRoleUseCase, EnableRoleUseCase>();
        services.AddScopedInternal<ISetRolePermissionsUseCase, SetRolePermissionsUseCase>();
        services.AddScopedInternal<IAddRolePermissionUseCase, AddRolePermissionUseCase>();
        services.AddScopedInternal<IRemoveRolePermissionUseCase, RemoveRolePermissionUseCase>();
        services.AddScopedInternal<IGetRoleQueryHandler, GetRoleQueryHandler>();
        services.AddScopedInternal<IListRolesQueryHandler, ListRolesQueryHandler>();
        services.AddScopedInternal<IAssignRoleUseCase, AssignRoleUseCase>();
        services.AddScopedInternal<IUnassignRoleUseCase, UnassignRoleUseCase>();
        services.AddScopedInternal<IGetUserRolesQueryHandler, GetUserRolesQueryHandler>();
    }

    private static void AddGroupUseCases(IServiceCollection services)
    {
        services.AddScopedInternal<ICreateGroupUseCase, CreateGroupUseCase>();
        services.AddScopedInternal<IUpdateGroupUseCase, UpdateGroupUseCase>();
        services.AddScopedInternal<IDeleteGroupUseCase, DeleteGroupUseCase>();
        services.AddScopedInternal<IAddUserToGroupUseCase, AddUserToGroupUseCase>();
        services.AddScopedInternal<IRemoveUserFromGroupUseCase, RemoveUserFromGroupUseCase>();
        services.AddScopedInternal<IAddGroupMappingUseCase, AddGroupMappingUseCase>();
        services.AddScopedInternal<IRemoveGroupMappingUseCase, RemoveGroupMappingUseCase>();

        services.AddScopedInternal<IGetGroupQueryHandler, GetGroupQueryHandler>();
        services.AddScopedInternal<IListGroupsQueryHandler, ListGroupsQueryHandler>();
        services.AddScopedInternal<IListGroupMembersQueryHandler, ListGroupMembersQueryHandler>();
        services.AddScopedInternal<IListGroupMappingsQueryHandler, ListGroupMappingsQueryHandler>();
        services.AddScopedInternal<IGetUserGroupsQueryHandler, GetUserGroupsQueryHandler>();
        services.AddScopedInternal<IUserGroupsProvider, RequestScopedUserGroupsProvider>();
        services.AddScoped<IGetGroupClaimsForUserQueryHandler, GetGroupClaimsForUserQueryHandler>();
    }

    private static void AddApplicationUseCases(IServiceCollection services)
    {
        services.AddScopedInternal<ApplicationLifecycleUseCases>();

        services.AddScopedInternal<ApplicationCredentialUseCases>();

        services.AddScopedInternal<ApplicationCredentialValidationUseCases>();
        services.AddScoped<IValidateApplicationClientCredentialsUseCase>(provider => provider.GetRequiredService<ApplicationCredentialValidationUseCases>());
        services.AddScoped<IValidateApplicationCertificateUseCase>(provider => provider.GetRequiredService<ApplicationCredentialValidationUseCases>());

        services.AddScopedInternal<ListApplicationsQueryHandler>();
        services.AddScoped<IListApplicationsQueryHandler>(provider => provider.GetRequiredService<ListApplicationsQueryHandler>());
        services.AddScopedInternal<GetApplicationQueryHandler>();
        services.AddScoped<IGetApplicationQueryHandler>(provider => provider.GetRequiredService<GetApplicationQueryHandler>());
        services.AddScopedInternal<ListApplicationCredentialsQueryHandler>();
        services.AddScoped<IListApplicationCredentialsQueryHandler>(provider => provider.GetRequiredService<ListApplicationCredentialsQueryHandler>());
        services.AddScoped<ListApplicationProfilePoliciesQueryHandler>();
        services.AddScoped<IListApplicationProfilePoliciesQueryHandler>(provider => provider.GetRequiredService<ListApplicationProfilePoliciesQueryHandler>());
        services.AddScoped<IApplicationsAdminWorkflow, ApplicationsAdminWorkflow>();
        services.AddScopedInternal<Resources.IResourcePermissionService, Resources.ResourcePermissionService>();
        services.AddScopedInternal<Resources.ResourceAccessWorkflow>();
    }

    private static void AddApplicationPermissionUseCases(IServiceCollection services)
    {
        services.AddScopedInternal<RegisterApplicationUseCase>();
        services.AddScopedInternal<IListRegisteredApplicationsQueryHandler, ListRegisteredApplicationsQueryHandler>();
        services.AddScopedInternal<IGetRegisteredApplicationQueryHandler, GetRegisteredApplicationQueryHandler>();
        services.AddScopedInternal<IListAssignablePermissionCatalogQueryHandler, ListAssignablePermissionCatalogQueryHandler>();

        services.AddScopedInternal<ApplicationPermissionManifestUseCases>();
        services.AddScopedInternal<ApplicationPermissionMaintenanceUseCases>();
        services.AddScoped<IApplicationPermissionRegistryWorkflow, ApplicationPermissionRegistryWorkflow>();
        services.AddScopedInternal<IGetPermissionDependenciesQueryHandler, GetPermissionDependenciesQueryHandler>();
    }

    private static void AddSessionAndLogoutUseCases(IServiceCollection services)
    {
        services.AddScopedInternal<ICreateSessionUseCase, CreateSessionUseCase>();
        services.AddScopedInternal<IAddClientSessionUseCase, AddClientSessionUseCase>();
        services.AddScopedInternal<IRevokeSessionUseCase, RevokeSessionUseCase>();
        services.AddScoped<IRevokeAllUserSessionsUseCase, RevokeAllUserSessionsUseCase>();
        services.AddScopedInternal<IListSessionsQueryHandler, ListSessionsQueryHandler>();
        services.AddScopedInternal<IGetSessionQueryHandler, GetSessionQueryHandler>();
        services.AddScopedInternal<IValidateSessionQueryHandler, ValidateSessionQueryHandler>();
        services.AddScopedInternal<ICredentialSessionValidator, CredentialSessionValidator>();

        services.AddScopedInternal<IProcessLogoutUseCase, ProcessLogoutUseCase>();
        services.AddScoped<INotifyClientsOfLogoutUseCase, NotifyClientsOfLogoutUseCase>();
    }

    private static void AddAuditUseCases(IServiceCollection services)
    {
        services.AddScoped<IListAuditEntriesQueryHandler, ListAuditEntriesQueryHandler>();
    }

    private static void AddProviderUseCases(IServiceCollection services)
    {
        services.AddScopedInternal<ICreateProviderUseCase, CreateProviderUseCase>();
        services.AddScopedInternal<IUpdateProviderUseCase, UpdateProviderUseCase>();
        services.AddScopedInternal<IListProvidersQueryHandler, ListProvidersQueryHandler>();
    }

    private static void AddAuthorizationServices(IServiceCollection services)
    {
        services.AddScopedInternal<IApplicationPermissionAuthorizationService, ApplicationPermissionAuthorizationService>();
        services.AddScoped<IApplicationPermissionAuditWriter, ApplicationPermissionAuditWriter>();
        services.AddScopedInternal<IPermissionAssignmentValidator, PermissionAssignmentValidator>();
        services.AddScopedInternal<IPermissionChecker, PermissionChecker>();
    }
}
