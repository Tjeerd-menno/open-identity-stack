using System.Reflection;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using OpenIdentityStack.Api.CurrentUser;

namespace OpenIdentityStack.Api.UnitTests.Endpoints;

public sealed class CurrentUserRouteMappingTests
{
    [Fact]
    public void MapCurrentUserApi_RegistersAuthenticatedCurrentUserEndpoint()
    {
        using WebApplication app = CreateApplication();

        app.MapCurrentUserApi();

        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(static dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => string.Equals(
                endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
                "GetCurrentUser",
                StringComparison.Ordinal));

        endpoint.RoutePattern.RawText.ShouldBe("api/me");
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.ShouldContain("GET");
        endpoint.Metadata.GetMetadata<ITagsMetadata>()?.Tags.ShouldContain(nameof(CurrentUserApi));

        IAuthorizeData authorizeData = endpoint.Metadata.OfType<IAuthorizeData>().Single();
        authorizeData.Policy.ShouldBeNull();
    }

    private static WebApplication CreateApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        return builder.Build();
    }
}
