using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace OpenIdentityStack.Api.Tests.Helpers;

/// <summary>
/// Helper methods for setting up HttpContext in unit tests.
/// </summary>
public static class HttpContextTestHelper
{
    /// <summary>
    /// Creates a DefaultHttpContext with mocked authentication services.
    /// Required when testing controllers that call SignOutAsync or SignInAsync.
    /// </summary>
    public static DefaultHttpContext CreateWithAuthenticationServices()
    {
        var httpContext = new DefaultHttpContext();
        ConfigureAuthenticationServices(httpContext);
        return httpContext;
    }

    /// <summary>
    /// Configures the given HttpContext with mocked authentication services.
    /// </summary>
    public static void ConfigureAuthenticationServices(HttpContext httpContext)
    {
        IAuthenticationService authenticationService = Substitute.For<IAuthenticationService>();

        // Configure SignOutAsync to return a completed task
        authenticationService.SignOutAsync(
                Arg.Any<HttpContext>(),
                Arg.Any<string?>(),
                Arg.Any<AuthenticationProperties?>())
            .Returns(Task.CompletedTask);

        // Configure SignInAsync to return a completed task
        authenticationService.SignInAsync(
                Arg.Any<HttpContext>(),
                Arg.Any<string?>(),
                Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
                Arg.Any<AuthenticationProperties?>())
            .Returns(Task.CompletedTask);

        // Configure ChallengeAsync to return a completed task
        authenticationService.ChallengeAsync(
                Arg.Any<HttpContext>(),
                Arg.Any<string?>(),
                Arg.Any<AuthenticationProperties?>())
            .Returns(Task.CompletedTask);

        // Configure ForbidAsync to return a completed task
        authenticationService.ForbidAsync(
                Arg.Any<HttpContext>(),
                Arg.Any<string?>(),
                Arg.Any<AuthenticationProperties?>())
            .Returns(Task.CompletedTask);

        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IAuthenticationService)).Returns(authenticationService);

        httpContext.RequestServices = serviceProvider;
    }
}
