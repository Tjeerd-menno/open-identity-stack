using System.Reflection;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Api.UnitTests.Endpoints;

public sealed class AdminApiRouteMappingTests
{
    private static readonly Assembly ApiAssembly = typeof(PermissionRequirement).Assembly;

    [Theory]
    [MemberData(nameof(GetApiExpectations))]
    public void MapApi_RegistersExpectedEndpoints(ApiExpectation expectation)
    {
        using WebApplication app = CreateApplication();

        InvokeMapMethod(expectation.TypeName, expectation.MapMethodName, app);

        RouteEndpoint[] endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ITagsMetadata>()?.Tags.Contains(expectation.Tag) == true)
            .ToArray();

        endpoints.Length.ShouldBe(expectation.Endpoints.Count);

        foreach (EndpointExpectation endpointExpectation in expectation.Endpoints)
        {
            RouteEndpoint endpoint = endpoints.Single(candidate =>
                string.Equals(
                    candidate.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
                    endpointExpectation.Name,
                    StringComparison.Ordinal));

            NormalizePattern(endpoint.RoutePattern.RawText).ShouldBe(NormalizePattern(endpointExpectation.Pattern));
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.ShouldContain(endpointExpectation.HttpMethod);
            endpoint.Metadata.OfType<IAuthorizeData>().Select(static metadata => metadata.Policy).ShouldContain(endpointExpectation.Policy);
        }
    }

    public static IEnumerable<object[]> GetApiExpectations()
    {
        yield return [new ApiExpectation(
            "OpenIdentityStack.Api.Clients.ClientsApi",
            "MapClientsApi",
            "ClientsApi",
            [
                new("CreateClient", "api/admin/clients", "POST", Permissions.Clients.Write),
                new("GetClient", "api/admin/clients/{id:guid}", "GET", Permissions.Clients.Read),
                new("ListClients", "api/admin/clients", "GET", Permissions.Clients.Read),
                new("UpdateClient", "api/admin/clients/{id:guid}", "PATCH", Permissions.Clients.Write),
                new("DeleteClient", "api/admin/clients/{id:guid}", "DELETE", Permissions.Clients.Delete)
            ])];

        yield return [new ApiExpectation(
            "OpenIdentityStack.Api.Users.UsersApi",
            "MapUsersApi",
            "UsersApi",
            [
                new("CreateUser", "api/admin/users", "POST", Permissions.Users.Write),
                new("GetUser", "api/admin/users/{id:guid}", "GET", Permissions.Users.Read),
                new("ListUsers", "api/admin/users", "GET", Permissions.Users.Read),
                new("UpdateUser", "api/admin/users/{id:guid}", "PUT", Permissions.Users.Write),
                new("DeleteUser", "api/admin/users/{id:guid}", "DELETE", Permissions.Users.Delete),
                new("DisableUser", "api/admin/users/{id:guid}/disable", "POST", Permissions.Users.Disable),
                new("EnableUser", "api/admin/users/{id:guid}/enable", "POST", Permissions.Users.Write),
                new("ResetPassword", "api/admin/users/{id:guid}/reset-password", "POST", Permissions.Users.ResetPassword),
                new("GetUserRoles", "api/admin/users/{id:guid}/roles", "GET", Permissions.Roles.Read),
                new("AssignRole", "api/admin/users/{userId:guid}/roles/{roleId:guid}", "POST", Permissions.Roles.Assign),
                new("UnassignRole", "api/admin/users/{userId:guid}/roles/{roleId:guid}", "DELETE", Permissions.Roles.Assign),
                new("GetUserGroups", "api/admin/users/{id:guid}/groups", "GET", Permissions.Groups.Read),
                new("GetUpstreamIdentities", "api/admin/users/{userId:guid}/upstream-identities", "GET", Permissions.Users.Read),
                new("LinkUpstreamIdentity", "api/admin/users/{userId:guid}/upstream-identities", "POST", Permissions.Users.Write),
                new("UnlinkUpstreamIdentity", "api/admin/users/{userId:guid}/upstream-identities/{providerId:guid}", "DELETE", Permissions.Users.Write)
            ])];

        yield return [new ApiExpectation(
            "OpenIdentityStack.Api.Admin.RolesApi",
            "MapRolesApi",
            "RolesApi",
            [
                new("ListRoles", "api/admin/roles", "GET", Permissions.Roles.Read),
                new("GetRole", "api/admin/roles/{id:guid}", "GET", Permissions.Roles.Read),
                new("CreateRole", "api/admin/roles", "POST", Permissions.Roles.Write),
                new("UpdateRole", "api/admin/roles/{id:guid}", "PUT", Permissions.Roles.Write),
                new("DeleteRole", "api/admin/roles/{id:guid}", "DELETE", Permissions.Roles.Write),
                new("DisableRole", "api/admin/roles/{id:guid}/disable", "POST", Permissions.Roles.Write),
                new("EnableRole", "api/admin/roles/{id:guid}/enable", "POST", Permissions.Roles.Write),
                new("GetRolePermissions", "api/admin/roles/{id:guid}/permissions", "GET", Permissions.Roles.Read),
                new("SetRolePermissions", "api/admin/roles/{id:guid}/permissions", "PUT", Permissions.Roles.Assign),
                new("AddRolePermission", "api/admin/roles/{id:guid}/permissions", "POST", Permissions.Roles.Assign),
                new("RemoveRolePermission", "api/admin/roles/{id:guid}/permissions/{permission}", "DELETE", Permissions.Roles.Assign)
            ])];

        yield return [new ApiExpectation(
            "OpenIdentityStack.Api.Groups.GroupsApi",
            "MapGroupsApi",
            "GroupsApi",
            [
                new("ListGroups", "api/admin/groups", "GET", Permissions.Groups.Read),
                new("CreateGroup", "api/admin/groups", "POST", Permissions.Groups.Write),
                new("GetGroup", "api/admin/groups/{id:guid}", "GET", Permissions.Groups.Read),
                new("UpdateGroup", "api/admin/groups/{id:guid}", "PATCH", Permissions.Groups.Write),
                new("DeleteGroup", "api/admin/groups/{id:guid}", "DELETE", Permissions.Groups.Delete),
                new("ListGroupMembers", "api/admin/groups/{id:guid}/members", "GET", Permissions.Groups.Read),
                new("AddGroupMember", "api/admin/groups/{id:guid}/members/{userId:guid}", "POST", Permissions.Groups.ManageMembers),
                new("RemoveGroupMember", "api/admin/groups/{id:guid}/members/{userId:guid}", "DELETE", Permissions.Groups.ManageMembers),
                new("ListGroupMappings", "api/admin/groups/{id:guid}/mappings", "GET", Permissions.Groups.Read),
                new("AddGroupMapping", "api/admin/groups/{id:guid}/mappings", "POST", Permissions.Groups.Write),
                new("RemoveGroupMapping", "api/admin/groups/{id:guid}/mappings/{mappingId:guid}", "DELETE", Permissions.Groups.Write)
            ])];

        yield return [new ApiExpectation(
            "OpenIdentityStack.Api.Sessions.SessionsApi",
            "MapSessionsApi",
            "SessionsApi",
            [
                new("ListSessions", "api/admin/sessions", "GET", Permissions.Sessions.Read),
                new("GetSession", "api/admin/sessions/{id:guid}", "GET", Permissions.Sessions.Read),
                new("RevokeSession", "api/admin/sessions/{id:guid}", "DELETE", Permissions.Sessions.Revoke),
                new("RevokeAllUserSessions", "api/admin/users/{userId:guid}/sessions", "DELETE", Permissions.Sessions.Revoke)
            ])];

        yield return [new ApiExpectation(
            "OpenIdentityStack.Api.ServiceAccounts.ServiceAccountsApi",
            "MapServiceAccountsApi",
            "ServiceAccountsApi",
            [
                new("CreateServiceAccount", "api/admin/service-accounts", "POST", Permissions.ServiceAccounts.Write),
                new("GetServiceAccount", "api/admin/service-accounts/{id:guid}", "GET", Permissions.ServiceAccounts.Read),
                new("UpdateServiceAccount", "api/admin/service-accounts/{id:guid}", "PATCH", Permissions.ServiceAccounts.Write),
                new("ListServiceAccounts", "api/admin/service-accounts", "GET", Permissions.ServiceAccounts.Read),
                new("DeleteServiceAccount", "api/admin/service-accounts/{id:guid}", "DELETE", Permissions.ServiceAccounts.Delete),
                new("DisableServiceAccount", "api/admin/service-accounts/{id:guid}/disable", "POST", Permissions.ServiceAccounts.Write),
                new("EnableServiceAccount", "api/admin/service-accounts/{id:guid}/enable", "POST", Permissions.ServiceAccounts.Write),
                new("RotateServiceAccountSecret", "api/admin/service-accounts/{id:guid}/rotate-secret", "POST", Permissions.ServiceAccounts.RotateSecret),
                new("AddServiceAccountCertificate", "api/admin/service-accounts/{id:guid}/certificates", "POST", Permissions.ServiceAccounts.ManageCertificates)
            ])];

        yield return [new ApiExpectation(
            "OpenIdentityStack.Api.Admin.ApplicationPermissionsApi",
            "MapApplicationPermissionsApi",
            "ApplicationPermissionsApi",
            [
                new("RegisterApplicationPermissionManifest", "api/admin/application-permissions/applications", "POST", Permissions.ApplicationPermissions.Write),
                new("ImportApplicationPermissionManifest", "api/admin/application-permissions/applications/import", "POST", Permissions.ApplicationPermissions.Write),
                new("ListRegisteredApplications", "api/admin/application-permissions/applications", "GET", Permissions.ApplicationPermissions.Read),
                new("GetRegisteredApplication", "api/admin/application-permissions/applications/{id:guid}", "GET", Permissions.ApplicationPermissions.Read),
                new("ListAssignablePermissionCatalog", "api/admin/application-permissions/catalog", "GET", Permissions.ApplicationPermissions.Read),
                new("UpdateRegisteredApplication", "api/admin/application-permissions/applications/{id:guid}", "PATCH", Permissions.ApplicationPermissions.Write),
                new("AddRegisteredApplicationPermission", "api/admin/application-permissions/applications/{id:guid}/permissions", "POST", Permissions.ApplicationPermissions.Write),
                new("UpdateRegisteredApplicationPermission", "api/admin/application-permissions/permissions/{permissionId:guid}", "PATCH", Permissions.ApplicationPermissions.Write),
                new("ChangeRegisteredApplicationLifecycle", "api/admin/application-permissions/applications/{id:guid}/lifecycle", "POST", Permissions.ApplicationPermissions.Write),
                new("ChangeRegisteredApplicationPermissionLifecycle", "api/admin/application-permissions/permissions/{permissionId:guid}/lifecycle", "POST", Permissions.ApplicationPermissions.Write),
                new("GetRegisteredApplicationPermissionDependencies", "api/admin/application-permissions/permissions/{permissionId:guid}/dependencies", "GET", Permissions.ApplicationPermissions.Read),
                new("TransferRegisteredApplicationOwnership", "api/admin/application-permissions/applications/{id:guid}/ownership", "POST", Permissions.ApplicationPermissions.Admin),
                new("AddRegisteredApplicationMaintainer", "api/admin/application-permissions/applications/{id:guid}/maintainers", "POST", Permissions.ApplicationPermissions.Admin),
                new("RemoveRegisteredApplicationMaintainer", "api/admin/application-permissions/applications/{id:guid}/maintainers/{principalId}", "DELETE", Permissions.ApplicationPermissions.Admin)
            ])];

        yield return [new ApiExpectation(
            "OpenIdentityStack.Api.Federation.ProvidersApi",
            "MapProvidersApi",
            "ProvidersApi",
            [
                new("ListProviders", "api/admin/providers", "GET", Permissions.Providers.Read),
                new("GetProvider", "api/admin/providers/{id:guid}", "GET", Permissions.Providers.Read),
                new("CreateProvider", "api/admin/providers", "POST", Permissions.Providers.Write),
                new("UpdateProvider", "api/admin/providers/{id:guid}", "PATCH", Permissions.Providers.Write),
                new("DeleteProvider", "api/admin/providers/{id:guid}", "DELETE", Permissions.Providers.Delete),
                new("DisableProvider", "api/admin/providers/{id:guid}/disable", "POST", Permissions.Providers.Write),
                new("EnableProvider", "api/admin/providers/{id:guid}/enable", "POST", Permissions.Providers.Write)
            ])];

        yield return [new ApiExpectation(
            "OpenIdentityStack.Api.Settings.AuthenticationSettingsApi",
            "MapAuthenticationSettingsApi",
            "AuthenticationSettingsApi",
            [
                new("GetAuthenticationSettings", "api/admin/authentication-settings", "GET", Permissions.System.ManageSettings),
                new("ListAuthenticationProviders", "api/admin/authentication-settings/providers", "GET", Permissions.System.ManageSettings),
                new("SetDefaultProvider", "api/admin/authentication-settings/default-provider", "PUT", Permissions.System.ManageSettings),
                new("SetLocalFallback", "api/admin/authentication-settings/local-fallback", "PUT", Permissions.System.ManageSettings)
            ])];
    }

    private static WebApplication CreateApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        return builder.Build();
    }


    private static string? NormalizePattern(string? pattern) =>
        string.IsNullOrEmpty(pattern) ? pattern : pattern.TrimEnd('/');

    private static void InvokeMapMethod(string typeName, string methodName, WebApplication app)
    {
        Type type = ApiAssembly.GetType(typeName) ?? throw new InvalidOperationException($"Could not find API type '{typeName}'.");
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Could not find map method '{methodName}' on '{typeName}'.");

        object? result = method.Invoke(null, [app]);
        result.ShouldBeSameAs(app);
    }

    public sealed record ApiExpectation(
        string TypeName,
        string MapMethodName,
        string Tag,
        IReadOnlyList<EndpointExpectation> Endpoints);

    public sealed record EndpointExpectation(
        string Name,
        string Pattern,
        string HttpMethod,
        string Policy);
}
