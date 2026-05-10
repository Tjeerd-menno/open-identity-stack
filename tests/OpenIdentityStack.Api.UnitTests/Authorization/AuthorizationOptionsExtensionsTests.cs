using Microsoft.AspNetCore.Authorization;

using OpenIddict.Validation.AspNetCore;

using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Api.UnitTests.Authorization;

public sealed class AuthorizationOptionsExtensionsTests
{
    [Fact]
    public void AddPermissionPolicies_AddsAdminPolicyWithBearerScheme()
    {
        AuthorizationOptions options = new();

        options.AddPermissionPolicies();

        AuthorizationPolicy? policy = options.GetPolicy(AuthorizationOptionsExtensions.AdminPolicy);

        policy.ShouldNotBeNull();
        policy.AuthenticationSchemes.ShouldContain(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        policy.Requirements.Any(static requirement => requirement.GetType().Name == "DenyAnonymousAuthorizationRequirement").ShouldBeTrue();
    }

    [Fact]
    public void AddPermissionPolicies_AddsPermissionPolicyWithRequirement()
    {
        AuthorizationOptions options = new();

        options.AddPermissionPolicies();

        AuthorizationPolicy? policy = options.GetPolicy(Permissions.Users.Read);
        PermissionRequirement? requirement = policy?.Requirements.OfType<PermissionRequirement>().SingleOrDefault();

        policy.ShouldNotBeNull();
        policy.AuthenticationSchemes.ShouldContain(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        requirement.ShouldNotBeNull();
        requirement.Permission.ShouldBe(Permissions.Users.Read);
    }
}
