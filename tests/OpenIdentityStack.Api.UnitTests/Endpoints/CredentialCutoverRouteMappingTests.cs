using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OpenIdentityStack.Api.Admin;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Security.Commands;

namespace OpenIdentityStack.Api.UnitTests.Endpoints;

public sealed class CredentialCutoverRouteMappingTests
{
    [Fact]
    public void ResourceWindowReviewAdvertisesMissingResourceProblem()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddApplication();
        builder.Services.AddScoped<CredentialCutoverReadiness>();
        builder.Services.AddScoped<IExecuteCredentialCutoverUseCase, ExecuteCredentialCutoverUseCase>();
        using WebApplication app = builder.Build();
        app.MapCredentialCutoverApi();
        RouteEndpoint endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(value => value.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "ReviewResourceTokenWindow");

        IProducesResponseTypeMetadata? missing = endpoint.Metadata.OfType<IProducesResponseTypeMetadata>()
            .SingleOrDefault(response => response.StatusCode == 404);

        missing.ShouldNotBeNull();
        missing.Type.ShouldBe(typeof(ProblemDetails));
        missing.ContentTypes.ShouldContain("application/problem+json");
    }
}
