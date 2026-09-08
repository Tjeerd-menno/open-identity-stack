using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class AccountLogoutFailureTests
{
    [Fact]
    public async Task Logout_WhenPersistenceThrows_SendsCookieDeletionHeadersWithServerError()
    {
        ICredentialTerminationService termination = Substitute.For<ICredentialTerminationService>();
        termination.TerminateSessionAsync(Arg.Any<SessionId>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result>(new InvalidOperationException("Persistence unavailable")));
        await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICredentialTerminationService>();
                services.AddSingleton(termination);
                services.AddSingleton<IStartupFilter, AuthenticatedSessionFilter>();
            });
        });
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        string confirmation = await client.GetStringAsync("/connect/logout");
        Match token = Regex.Match(confirmation, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);

        using HttpResponseMessage response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value) }));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        string[] cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, cookie => cookie.StartsWith(".AspNetCore.Cookies=;", StringComparison.Ordinal)
            && cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies, cookie => cookie.StartsWith("op_session=;", StringComparison.Ordinal)
            && cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        await termination.Received(1).TerminateSessionAsync(Arg.Any<SessionId>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    private sealed class AuthenticatedSessionFilter : IStartupFilter
    {
        private readonly ClaimsPrincipal principal = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim("sid", Guid.NewGuid().ToString())], "Cookies"));

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.User = this.principal;
                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
