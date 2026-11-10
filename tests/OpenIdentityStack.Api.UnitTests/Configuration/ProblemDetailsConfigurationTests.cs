using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenIdentityStack.Api.Configuration;

namespace OpenIdentityStack.Api.UnitTests.Configuration;

public sealed class ProblemDetailsConfigurationTests
{
    [Fact]
    public void ConcurrencyFailureProducesRetryableConflictWithoutInternalDetails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new ProductionEnvironment());
        services.AddConfiguredProblemDetails();
        using ServiceProvider provider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Request.Path = "/api/admin/providers/provider-id";
        var problem = new ProblemDetails();
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = new DbUpdateConcurrencyException("database implementation detail")
        };

        provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails!(context);

        problem.Status.ShouldBe(StatusCodes.Status409Conflict);
        problem.Title.ShouldBe("Conflict");
        problem.Type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.5.8");
        problem.Detail.ShouldBe("The state changed while this request was being processed. Reload and retry the operation.");
        problem.Detail.ShouldNotBeNull().ShouldNotContain("database implementation detail");
    }

    [Fact]
    public void MalformedJsonPreservesBadRequestWithoutInternalDetails()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new ProductionEnvironment());
        services.AddConfiguredProblemDetails();
        using ServiceProvider provider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        var problem = new ProblemDetails { Status = StatusCodes.Status400BadRequest };
        var context = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = new JsonException("internal request type and property details")
        };

        provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value.CustomizeProblemDetails!(context);

        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Status.ShouldBe(StatusCodes.Status400BadRequest);
        problem.Title.ShouldBe("Bad Request");
        problem.Detail.ShouldBe("The request body is invalid or malformed.");
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = nameof(ProblemDetailsConfigurationTests);
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
