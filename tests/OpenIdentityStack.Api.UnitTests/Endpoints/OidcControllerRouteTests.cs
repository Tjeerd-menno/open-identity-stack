using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenIdentityStack.Api.Authentication;

namespace OpenIdentityStack.Api.UnitTests.Endpoints;

public sealed class OidcControllerRouteTests
{
    [Theory]
    [MemberData(nameof(GetOidcEndpointExpectations))]
    public void MapControllers_RegistersOidcEndpoints(OidcEndpointExpectation expectation)
    {
        using WebApplication app = CreateApplication();

        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate =>
            {
                ControllerActionDescriptor? action = candidate.Metadata.GetMetadata<ControllerActionDescriptor>();
                return string.Equals(action?.ControllerName, expectation.Controller, StringComparison.Ordinal)
                    && string.Equals(action?.ActionName, expectation.Action, StringComparison.Ordinal)
                    && candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(expectation.HttpMethod) == true;
            });

        NormalizePattern(endpoint.RoutePattern.RawText).ShouldBe(expectation.Pattern);
    }

    public static IEnumerable<object[]> GetOidcEndpointExpectations()
    {
        yield return [new OidcEndpointExpectation("Authorization", "Authorize", "GET", "connect/authorize")];
        yield return [new OidcEndpointExpectation("Authorization", "Authorize", "POST", "connect/authorize")];
        yield return [new OidcEndpointExpectation("Authorization", "Exchange", "POST", "connect/token")];
        yield return [new OidcEndpointExpectation("Authorization", "UserInfo", "GET", "connect/userinfo")];
        yield return [new OidcEndpointExpectation("Authorization", "UserInfo", "POST", "connect/userinfo")];
        yield return [new OidcEndpointExpectation("Logout", "Logout", "GET", "connect/logout")];
        yield return [new OidcEndpointExpectation("Logout", "Logout", "POST", "connect/logout")];
    }

    private static WebApplication CreateApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        IMvcBuilder mvcBuilder = builder.Services.AddControllers();
        mvcBuilder.PartManager.ApplicationParts.Add(new AssemblyPart(typeof(AuthorizationController).Assembly));

        WebApplication app = builder.Build();
        app.MapControllers();
        return app;
    }

    private static string? NormalizePattern(string? pattern) =>
        string.IsNullOrEmpty(pattern) ? pattern : pattern.TrimStart('~', '/').TrimEnd('/');

    public sealed record OidcEndpointExpectation(
        string Controller,
        string Action,
        string HttpMethod,
        string Pattern);
}
