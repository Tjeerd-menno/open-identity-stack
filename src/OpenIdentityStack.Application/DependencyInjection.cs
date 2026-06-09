using Microsoft.Extensions.DependencyInjection;

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
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.ServiceAccounts.Commands;
using OpenIdentityStack.Application.ServiceAccounts.Queries;
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
        AddServiceAccountUseCases(services);
        AddApplicationUseCases(services);
        AddApplicationPermissionUseCases(services);
        AddSessionAndLogoutUseCases(services);
        AddAuditUseCases(services);
        AddProviderUseCases(services);
        AddAuthorizationServices(services);

        return services;
    }

    private static void AddUserUseCases(IServiceCollection services)
    {
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
        services.AddScoped<IGetUserEffectiveRolesQueryHandler, GetUserEffectiveRolesQueryHandler>();
    }

    private static void AddFederationUseCases(IServiceCollection services)
    {
        services.AddScoped<IJitProvisionUserUseCase, JitProvisionUserUseCase>();
        services.AddScoped<ILinkUpstreamIdentityUseCase, LinkUpstreamIdentityUseCase>();
        services.AddScoped<IUnlinkUpstreamIdentityUseCase, UnlinkUpstreamIdentityUseCase>();
        services.AddScoped<IFindUserByUpstreamIdentityQueryHandler, FindUserByUpstreamIdentityQueryHandler>();
        services.AddScoped<IListUserUpstreamIdentitiesQueryHandler, ListUserUpstreamIdentitiesQueryHandler>();
    }

    private static void AddRoleUseCases(IServiceCollection services)
    {
        services.AddScoped<ICreateRoleUseCase, CreateRoleUseCase>();
        services.AddScoped<IListRolesQueryHandler, ListRolesQueryHandler>();
        services.AddScoped<IAssignRoleUseCase, AssignRoleUseCase>();
        services.AddScoped<IUnassignRoleUseCase, UnassignRoleUseCase>();
        services.AddScoped<IGetUserRolesQueryHandler, GetUserRolesQueryHandler>();
    }

    private static void AddGroupUseCases(IServiceCollection services)
    {
        services.AddScoped<ICreateGroupUseCase, CreateGroupUseCase>();
        services.AddScoped<IUpdateGroupUseCase, UpdateGroupUseCase>();
        services.AddScoped<IDeleteGroupUseCase, DeleteGroupUseCase>();
        services.AddScoped<IAddUserToGroupUseCase, AddUserToGroupUseCase>();
        services.AddScoped<IRemoveUserFromGroupUseCase, RemoveUserFromGroupUseCase>();
        services.AddScoped<IAddGroupMappingUseCase, AddGroupMappingUseCase>();
        services.AddScoped<IRemoveGroupMappingUseCase, RemoveGroupMappingUseCase>();

        services.AddScoped<IGetGroupQueryHandler, GetGroupQueryHandler>();
        services.AddScoped<IListGroupsQueryHandler, ListGroupsQueryHandler>();
        services.AddScoped<IListGroupMembersQueryHandler, ListGroupMembersQueryHandler>();
        services.AddScoped<IListGroupMappingsQueryHandler, ListGroupMappingsQueryHandler>();
        services.AddScoped<IGetUserGroupsQueryHandler, GetUserGroupsQueryHandler>();
        services.AddScoped<IGetGroupClaimsForUserQueryHandler, GetGroupClaimsForUserQueryHandler>();
    }

    private static void AddServiceAccountUseCases(IServiceCollection services)
    {
        services.AddScoped<ICreateServiceAccountUseCase, CreateServiceAccountUseCase>();
        services.AddScoped<IUpdateServiceAccountUseCase, UpdateServiceAccountUseCase>();
        services.AddScoped<IDeleteServiceAccountUseCase, DeleteServiceAccountUseCase>();
        services.AddScoped<IListServiceAccountsQueryHandler, ListServiceAccountsQueryHandler>();
        services.AddScoped<IGetServiceAccountQueryHandler, GetServiceAccountQueryHandler>();
        services.AddScoped<IRotateSecretUseCase, RotateSecretUseCase>();
        services.AddScoped<IAddCertificateUseCase, AddCertificateUseCase>();
        services.AddScoped<IDisableServiceAccountUseCase, DisableServiceAccountUseCase>();
        services.AddScoped<IEnableServiceAccountUseCase, EnableServiceAccountUseCase>();
    }

    private static void AddApplicationUseCases(IServiceCollection services)
    {
        services.AddScoped<ApplicationLifecycleUseCases>();

        services.AddScoped<ApplicationCredentialUseCases>();

        services.AddScoped<ApplicationCredentialValidationUseCases>();
        services.AddScoped<IValidateApplicationClientCredentialsUseCase>(provider => provider.GetRequiredService<ApplicationCredentialValidationUseCases>());
        services.AddScoped<IValidateApplicationCertificateUseCase>(provider => provider.GetRequiredService<ApplicationCredentialValidationUseCases>());

        services.AddScoped<ListApplicationsQueryHandler>();
        services.AddScoped<IListApplicationsQueryHandler>(provider => provider.GetRequiredService<ListApplicationsQueryHandler>());
        services.AddScoped<GetApplicationQueryHandler>();
        services.AddScoped<IGetApplicationQueryHandler>(provider => provider.GetRequiredService<GetApplicationQueryHandler>());
        services.AddScoped<ListApplicationCredentialsQueryHandler>();
        services.AddScoped<IListApplicationCredentialsQueryHandler>(provider => provider.GetRequiredService<ListApplicationCredentialsQueryHandler>());
        services.AddScoped<ListApplicationProfilePoliciesQueryHandler>();
        services.AddScoped<IListApplicationProfilePoliciesQueryHandler>(provider => provider.GetRequiredService<ListApplicationProfilePoliciesQueryHandler>());
        services.AddScoped<IApplicationsAdminWorkflow, ApplicationsAdminWorkflow>();
    }

    private static void AddApplicationPermissionUseCases(IServiceCollection services)
    {
        services.AddScoped<RegisterApplicationUseCase>();
        services.AddScoped<IRegisterApplicationUseCase>(provider => provider.GetRequiredService<RegisterApplicationUseCase>());
        services.AddScoped<IRegisterPermissionManifestUseCase>(provider => provider.GetRequiredService<RegisterApplicationUseCase>());
        services.AddScoped<IListRegisteredApplicationsQueryHandler, ListRegisteredApplicationsQueryHandler>();
        services.AddScoped<IGetRegisteredApplicationQueryHandler, GetRegisteredApplicationQueryHandler>();
        services.AddScoped<IListAssignablePermissionCatalogQueryHandler, ListAssignablePermissionCatalogQueryHandler>();

        services.AddScoped<ApplicationPermissionManifestUseCases>();
        services.AddScoped<ApplicationPermissionMaintenanceUseCases>();
        services.AddScoped<IUpdateRegisteredApplicationUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IAddApplicationPermissionUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IUpdateApplicationPermissionUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IDeleteApplicationPermissionUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IPreviewApplicationPermissionDeletionImpactUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IDeleteRegisteredApplicationUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IPreviewRegisteredApplicationDeletionImpactUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IChangeRegisteredApplicationLifecycleUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<ITransferRegisteredApplicationOwnershipUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IAddDelegatedMaintainerUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IRemoveDelegatedMaintainerUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IUpdateRemovedPermissionReplacementUseCase>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IListApplicationPermissionHistoryQueryHandler>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IListApplicationPermissionDiagnosticsQueryHandler>(provider => provider.GetRequiredService<ApplicationPermissionMaintenanceUseCases>());
        services.AddScoped<IGetPermissionDependenciesQueryHandler, GetPermissionDependenciesQueryHandler>();
    }

    private static void AddSessionAndLogoutUseCases(IServiceCollection services)
    {
        services.AddScoped<ICreateSessionUseCase, CreateSessionUseCase>();
        services.AddScoped<IAddClientSessionUseCase, AddClientSessionUseCase>();
        services.AddScoped<IRevokeSessionUseCase, RevokeSessionUseCase>();
        services.AddScoped<IRevokeAllUserSessionsUseCase, RevokeAllUserSessionsUseCase>();
        services.AddScoped<IListSessionsQueryHandler, ListSessionsQueryHandler>();
        services.AddScoped<IValidateSessionQueryHandler, ValidateSessionQueryHandler>();

        services.AddScoped<IProcessLogoutUseCase, ProcessLogoutUseCase>();
        services.AddScoped<INotifyClientsOfLogoutUseCase, NotifyClientsOfLogoutUseCase>();
    }

    private static void AddAuditUseCases(IServiceCollection services)
    {
        services.AddScoped<IListAuditEntriesQueryHandler, ListAuditEntriesQueryHandler>();
    }

    private static void AddProviderUseCases(IServiceCollection services)
    {
        services.AddScoped<ICreateProviderUseCase, CreateProviderUseCase>();
        services.AddScoped<IUpdateProviderUseCase, UpdateProviderUseCase>();
        services.AddScoped<IListProvidersQueryHandler, ListProvidersQueryHandler>();
    }

    private static void AddAuthorizationServices(IServiceCollection services)
    {
        services.AddScoped<IApplicationPermissionAuthorizationService, ApplicationPermissionAuthorizationService>();
        services.AddScoped<IApplicationPermissionAuditWriter, ApplicationPermissionAuditWriter>();
        services.AddScoped<IPermissionAssignmentValidator, PermissionAssignmentValidator>();
        services.AddScoped<IPermissionClaimProjectionService, PermissionClaimProjectionService>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();
    }
}
