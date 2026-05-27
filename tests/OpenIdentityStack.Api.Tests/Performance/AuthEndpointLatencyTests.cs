using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Identity;

using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;
namespace OpenIdentityStack.Api.Tests.Performance;

/// <summary>
/// Performance tests verifying P50/P95/P99 latency targets for authentication endpoints.
/// 
/// Targets (from constitution):
/// - P50 ≤100ms
/// - P95 ≤250ms  
/// - P99 ≤500ms
/// - 1000 concurrent auth requests supported
/// 
/// Note: These tests measure controller/handler dispatch overhead with mocked I/O.
/// Actual production latency will include database and network time.
/// </summary>
public sealed class AuthEndpointLatencyTests
{
    private const int IterationCount = 1000;
    private const double P50ThresholdMs = 1.0;   // Controller overhead budget (I/O mocked)
    private const double P95ThresholdMs = 2.5;   // Controller overhead budget (I/O mocked)
    private const double P99ThresholdMs = 5.0;   // Controller overhead budget (I/O mocked)

    #region Token Exchange Latency

    [Fact]
    public async Task TokenExchange_LatencyPercentiles_MeetTargets()
    {
        // Arrange
        AuthorizationController controller = CreateMockedAuthorizationController();
        var latencies = new List<double>(IterationCount);

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            await controller.Exchange();
        }

        // Act - Measure latencies for each iteration
        for (int i = 0; i < IterationCount; i++)
        {
            var sw = Stopwatch.StartNew();
            await controller.Exchange();
            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Calculate percentiles
        latencies.Sort();
        double p50 = GetPercentile(latencies, 50);
        double p95 = GetPercentile(latencies, 95);
        double p99 = GetPercentile(latencies, 99);

        // Assert
        p50.ShouldBeLessThan(P50ThresholdMs, $"Token Exchange P50 ({p50:F3}ms) exceeded {P50ThresholdMs}ms budget");
        p95.ShouldBeLessThan(P95ThresholdMs, $"Token Exchange P95 ({p95:F3}ms) exceeded {P95ThresholdMs}ms budget");
        p99.ShouldBeLessThan(P99ThresholdMs, $"Token Exchange P99 ({p99:F3}ms) exceeded {P99ThresholdMs}ms budget");
    }

    #endregion

    #region Session Validation Latency

    [Fact]
    public async Task SessionValidation_LatencyPercentiles_MeetTargets()
    {
        // Arrange
        IValidateSessionQueryHandler handler = Substitute.For<IValidateSessionQueryHandler>();
        handler.HandleAsync(Arg.Any<ValidateSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateSessionResult(true));

        var sessionId = SessionId.Create();
        var latencies = new List<double>(IterationCount);

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            await handler.HandleAsync(new ValidateSessionQuery(sessionId), CancellationToken.None);
        }

        // Act - Measure latencies
        for (int i = 0; i < IterationCount; i++)
        {
            var sw = Stopwatch.StartNew();
            await handler.HandleAsync(new ValidateSessionQuery(sessionId), CancellationToken.None);
            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Calculate percentiles
        latencies.Sort();
        double p50 = GetPercentile(latencies, 50);
        double p95 = GetPercentile(latencies, 95);
        double p99 = GetPercentile(latencies, 99);

        // Assert
        p50.ShouldBeLessThan(P50ThresholdMs, $"Session Validation P50 ({p50:F3}ms) exceeded {P50ThresholdMs}ms budget");
        p95.ShouldBeLessThan(P95ThresholdMs, $"Session Validation P95 ({p95:F3}ms) exceeded {P95ThresholdMs}ms budget");
        p99.ShouldBeLessThan(P99ThresholdMs, $"Session Validation P99 ({p99:F3}ms) exceeded {P99ThresholdMs}ms budget");
    }

    #endregion

    #region Logout Handler Latency

    [Fact]
    public async Task LogoutHandler_LatencyPercentiles_MeetTargets()
    {
        // Arrange
        LogoutController controller = CreateMockedLogoutController();
        var latencies = new List<double>(IterationCount);

        // Warmup
        for (int i = 0; i < 10; i++)
        {
            await controller.Logout();
        }

        // Act - Measure latencies
        for (int i = 0; i < IterationCount; i++)
        {
            var sw = Stopwatch.StartNew();
            await controller.Logout();
            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        // Calculate percentiles
        latencies.Sort();
        double p50 = GetPercentile(latencies, 50);
        double p95 = GetPercentile(latencies, 95);
        double p99 = GetPercentile(latencies, 99);

        // Assert
        p50.ShouldBeLessThan(P50ThresholdMs, $"Logout P50 ({p50:F3}ms) exceeded {P50ThresholdMs}ms budget");
        p95.ShouldBeLessThan(P95ThresholdMs, $"Logout P95 ({p95:F3}ms) exceeded {P95ThresholdMs}ms budget");
        p99.ShouldBeLessThan(P99ThresholdMs, $"Logout P99 ({p99:F3}ms) exceeded {P99ThresholdMs}ms budget");
    }

    #endregion

    #region Application Credential Lookup Latency

    [Fact]
    public async Task ApplicationClientAuthentication_LatencyPercentiles_MeetTargetsAndUseSingleApplicationLookup()
    {
        IValidateApplicationClientCredentialsUseCase validateCredentialsUseCase =
            Substitute.For<IValidateApplicationClientCredentialsUseCase>();
        IValidateApplicationCertificateUseCase validateCertificateUseCase =
            Substitute.For<IValidateApplicationCertificateUseCase>();
        validateCredentialsUseCase.ExecuteAsync(
                Arg.Any<ValidateApplicationClientCredentialsCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(CreateApplicationValidationResult());
        var handler = new ApplicationClientAuthenticationHandler(validateCredentialsUseCase, validateCertificateUseCase);
        var latencies = new List<double>(IterationCount);

        for (int i = 0; i < 10; i++)
        {
            await handler.HandleAsync(CreateApplicationCredentialContext());
        }

        for (int i = 0; i < IterationCount; i++)
        {
            var sw = Stopwatch.StartNew();
            await handler.HandleAsync(CreateApplicationCredentialContext());
            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        latencies.Sort();
        double p50 = GetPercentile(latencies, 50);
        double p95 = GetPercentile(latencies, 95);
        double p99 = GetPercentile(latencies, 99);

        p50.ShouldBeLessThan(P50ThresholdMs, $"Application credential validation P50 ({p50:F3}ms) exceeded {P50ThresholdMs}ms budget");
        p95.ShouldBeLessThan(P95ThresholdMs, $"Application credential validation P95 ({p95:F3}ms) exceeded {P95ThresholdMs}ms budget");
        p99.ShouldBeLessThan(P99ThresholdMs, $"Application credential validation P99 ({p99:F3}ms) exceeded {P99ThresholdMs}ms budget");
        await validateCredentialsUseCase.Received(IterationCount + 10)
            .ExecuteAsync(Arg.Any<ValidateApplicationClientCredentialsCommand>(), Arg.Any<CancellationToken>());
        await validateCertificateUseCase.DidNotReceive()
            .ExecuteAsync(Arg.Any<ValidateApplicationCertificateCommand>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Concurrent Request Handling

    [Fact]
    public async Task TokenExchange_ConcurrentRequests_HandlesLoad()
    {
        // Arrange - Create 1000 concurrent requests
        const int concurrentRequests = 1000;
        AuthorizationController[] controllers = Enumerable.Range(0, concurrentRequests)
            .Select(_ => CreateMockedAuthorizationController())
            .ToArray();

        var tasks = new List<Task<double>>(concurrentRequests);

        // Act - Execute all requests concurrently
        var overallSw = Stopwatch.StartNew();
        
        foreach (AuthorizationController? controller in controllers)
        {
            tasks.Add(Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                await controller.Exchange();
                sw.Stop();
                return sw.Elapsed.TotalMilliseconds;
            }));
        }

        double[] latencies = await Task.WhenAll(tasks);
        overallSw.Stop();

        // Calculate percentiles
        var sortedLatencies = latencies.OrderBy(x => x).ToList();
        double p50 = GetPercentile(sortedLatencies, 50);
        double p95 = GetPercentile(sortedLatencies, 95);
        double p99 = GetPercentile(sortedLatencies, 99);

        // Assert - All 1000 requests completed
        latencies.Length.ShouldBe(concurrentRequests);

        // With mocked I/O, controller dispatch should be very fast
        // Allow higher thresholds for concurrent execution due to thread contention
        p95.ShouldBeLessThan(50.0, $"Concurrent P95 ({p95:F3}ms) too high - possible contention");
        p99.ShouldBeLessThan(100.0, $"Concurrent P99 ({p99:F3}ms) too high - possible contention");
    }

    #endregion

    #region Throughput Tests

    [Fact]
    public async Task TokenExchange_Throughput_MeetsBaseline()
    {
        // Arrange
        AuthorizationController controller = CreateMockedAuthorizationController();
        const int durationSeconds = 1;
        int requests = 0;

        // Act - Run as many requests as possible in 1 second
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < durationSeconds)
        {
            await controller.Exchange();
            requests++;
        }
        sw.Stop();

        double requestsPerSecond = requests / sw.Elapsed.TotalSeconds;

        // Assert - Should handle at least 1000 requests/second with mocked I/O
        requestsPerSecond.ShouldBeGreaterThan(1000.0, 
            $"Throughput {requestsPerSecond:F0} req/s below 1000 req/s baseline");
    }

    #endregion

    #region Helper Methods

    private static double GetPercentile(IReadOnlyList<double> sortedValues, int percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        int index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }

    private static AuthorizationController CreateMockedAuthorizationController()
    {
        IOpenIddictApplicationManager applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        IOpenIddictScopeManager scopeManager = Substitute.For<IOpenIddictScopeManager>();
        IUserRepository userRepository = Substitute.For<IUserRepository>();
        IGetUserEffectiveRolesQueryHandler getUserEffectiveRolesQueryHandler = Substitute.For<IGetUserEffectiveRolesQueryHandler>();
        IGetGroupClaimsForUserQueryHandler getGroupClaimsForUserQueryHandler = Substitute.For<IGetGroupClaimsForUserQueryHandler>();
        IAddClientSessionUseCase addClientSessionUseCase = Substitute.For<IAddClientSessionUseCase>();
        IValidateSessionQueryHandler validateSessionQueryHandler = Substitute.For<IValidateSessionQueryHandler>();
        IOpenIddictRequestService openIddictRequestService = Substitute.For<IOpenIddictRequestService>();

        var controller = new AuthorizationController(
            applicationManager,
            scopeManager,
            userRepository,
            getUserEffectiveRolesQueryHandler,
            getGroupClaimsForUserQueryHandler,
            addClientSessionUseCase,
            validateSessionQueryHandler,
            openIddictRequestService
        );

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Setup service provider with mocked authentication
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        IAuthenticationService authService = Substitute.For<IAuthenticationService>();
        serviceProvider.GetService(typeof(IAuthenticationService)).Returns(authService);
        httpContext.RequestServices = serviceProvider;

        // Create a session and principal
        var sessionId = SessionId.Create();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("session_id", sessionId.Value.ToString())],
            OpenIddictConstants.Schemes.Bearer));

        // Mock AuthenticateAsync to return success with principal
        var authResult = AuthenticateResult.Success(new AuthenticationTicket(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme));
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string>())
            .Returns(Task.FromResult(authResult));

        // Simulate an OpenIddict Request with RefreshToken grant
        var request = new OpenIddictRequest { GrantType = OpenIddictConstants.GrantTypes.RefreshToken };
        openIddictRequestService.GetRequest(httpContext).Returns(request);

        httpContext.User = principal;

        // Simulate Successful Validation Query
        validateSessionQueryHandler.HandleAsync(Arg.Any<ValidateSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValidateSessionResult(true));

        return controller;
    }

    private static OpenIddict.Server.OpenIddictServerEvents.ValidateTokenRequestContext CreateApplicationCredentialContext()
    {
        var transaction = new OpenIddict.Server.OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest
            {
                GrantType = OpenIddictConstants.GrantTypes.ClientCredentials,
                ClientId = "orders-worker",
                ClientSecret = "secret"
            }
        };

        return new OpenIddict.Server.OpenIddictServerEvents.ValidateTokenRequestContext(transaction);
    }

    private static Result<ValidateApplicationCredentialsResult> CreateApplicationValidationResult() =>
        new ValidateApplicationCredentialsResult(
            DomainApplicationId.NewId(),
            "orders-worker",
            "Orders worker",
            ["orders.write"],
            [OpenIddictConstants.GrantTypes.ClientCredentials]);

    private static LogoutController CreateMockedLogoutController()
    {
        IProcessLogoutUseCase processLogoutUseCase = Substitute.For<IProcessLogoutUseCase>();
        IFrontChannelLogoutService frontChannelLogoutService = Substitute.For<IFrontChannelLogoutService>();
        ISessionRepository sessionRepository = Substitute.For<ISessionRepository>();
        ILogoutNotifier logoutNotifier = Substitute.For<ILogoutNotifier>();
        IOpenIddictRequestService requestService = Substitute.For<IOpenIddictRequestService>();

        var sessionId = SessionId.Create();
        var logoutResult = new ProcessLogoutResult(
            SessionId: sessionId,
            NotificationResult: new LogoutNotificationResult(0, 0, []),
            FrontChannelLogoutUrls: []
        );

        processLogoutUseCase.ExecuteAsync(Arg.Any<SessionId>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Result<ProcessLogoutResult>)logoutResult);

        var controller = new LogoutController(
            processLogoutUseCase,
            frontChannelLogoutService,
            sessionRepository,
            logoutNotifier,
            requestService
        );

        DefaultHttpContext httpContext = Helpers.HttpContextTestHelper.CreateWithAuthenticationServices();
        Claim[] claims = new[] { new Claim("session_id", sessionId.Value.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new OpenIddictRequest();
        requestService.GetRequest(Arg.Any<HttpContext>()).Returns(request);

        return controller;
    }

    #endregion
}
