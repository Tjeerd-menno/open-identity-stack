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
    [Theory]
    [InlineData("GetCredentialCutoverReadiness", 401)]
    [InlineData("GetCredentialCutoverReadiness", 403)]
    [InlineData("RecordEmergencyAccessEvidence", 401)]
    [InlineData("ReviewResourceTokenWindow", 401)]
    public void ReadinessRoutesAdvertiseAuthorizationProblems(string endpointName, int statusCode)
    {
        RouteEndpoint endpoint = MapEndpoints().Single(value =>
            value.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == endpointName);

        IProducesResponseTypeMetadata? response = endpoint.Metadata.OfType<IProducesResponseTypeMetadata>()
            .SingleOrDefault(candidate => candidate.StatusCode == statusCode);

        response.ShouldNotBeNull();
        response.Type.ShouldBe(typeof(ProblemDetails));
        response.ContentTypes.ShouldContain("application/problem+json");
    }

    [Fact]
    public void ResourceWindowReviewAdvertisesMissingResourceProblem()
    {
        RouteEndpoint endpoint = MapEndpoints()
            .Single(value => value.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "ReviewResourceTokenWindow");

        IProducesResponseTypeMetadata? missing = endpoint.Metadata.OfType<IProducesResponseTypeMetadata>()
            .SingleOrDefault(response => response.StatusCode == 404);

        missing.ShouldNotBeNull();
        missing.Type.ShouldBe(typeof(ProblemDetails));
        missing.ContentTypes.ShouldContain("application/problem+json");
    }

    private static IReadOnlyList<RouteEndpoint> MapEndpoints()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddApplication();
        builder.Services.AddScoped<CredentialCutoverReadiness>();
        builder.Services.AddScoped<IExecuteCredentialCutoverUseCase, ExecuteCredentialCutoverUseCase>();
        using WebApplication app = builder.Build();
        app.MapCredentialCutoverApi();
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
    }
}
