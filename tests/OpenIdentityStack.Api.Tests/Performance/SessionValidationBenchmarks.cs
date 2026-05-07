using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Performance;

public class SessionValidationBenchmarks
{
    [Fact]
    public async Task ValidateSession_Benchmark()
    {
        // 1. Arrange - Setup minimal mocks for the "Hot Path" (Token Exchange)
        IOpenIddictApplicationManager applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        IOpenIddictScopeManager scopeManager = Substitute.For<IOpenIddictScopeManager>();
        IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
        IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler = Substitute.For<IGetGroupClaimsForUserQueryHandler>();
        IAddClientSessionUseCase addClientSessionUseCase = Substitute.For<OpenIdentityStack.Application.Sessions.Commands.IAddClientSessionUseCase>();
        IValidateSessionQueryHandler validateSessionQueryHandler = Substitute.For<IValidateSessionQueryHandler>();
        IOpenIddictRequestService openIddictRequestService = Substitute.For<IOpenIddictRequestService>();
        
        var controller = new AuthorizationController(
            applicationManager,
            scopeManager,
            getUserEffectiveRolesQueryHandler,
            getGroupClaimsForUserQueryHandler,
            addClientSessionUseCase,
            validateSessionQueryHandler,
            openIddictRequestService
        );

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        IAuthenticationService authService = Substitute.For<IAuthenticationService>();
        serviceProvider.GetService(typeof(IAuthenticationService)).Returns(authService);
        httpContext.RequestServices = serviceProvider;

        // Simulate an OpenIddict Request
        var request = new OpenIddictRequest();
        request.GrantType = OpenIddictConstants.GrantTypes.RefreshToken;
        openIddictRequestService.GetRequest(httpContext).Returns(request);

        // Simulate User Principal with Session Claim
        var sessionId = SessionId.Create();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("session_id", sessionId.Value.ToString())], 
            OpenIddictConstants.Schemes.Bearer)); // Using Bearer as a safe default if Server is missing
        
        httpContext.User = principal;

        // Mock AuthenticateAsync to return success with principal
        var authResult = AuthenticateResult.Success(new AuthenticationTicket(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme));
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string>())
            .Returns(Task.FromResult(authResult));

        // Simulate Successful Validation Query
        validateSessionQueryHandler.HandleAsync(Arg.Any<ValidateSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateSessionResult(true));

        // Warmup
        await controller.Exchange();

        // 2. Act - Measure 1000 Iterations
        var stopwatch = Stopwatch.StartNew();
        const int iterations = 1000;
        
        for (int i = 0; i < iterations; i++)
        {
            await controller.Exchange();
        }

        stopwatch.Stop();

        // 3. Assert - Latency Budget
        double avgTimeMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
        
        // This assertion ensures the controller overhead itself is negligible (< 1ms)
        // Note: This measures pure dispatch overhead, not actual I/O, which is mocked.
        Assert.True(avgTimeMs < 1.0, $"Average dispatch time {avgTimeMs}ms exceeded budget of 1.0ms");
    }
}
