using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authentication;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Tests.Authentication;

public sealed class InteractiveSignInTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly ISessionRepository sessions = Substitute.For<ISessionRepository>();
    private readonly IAuthenticationSettingsRepository settings = Substitute.For<IAuthenticationSettingsRepository>();
    private readonly IUpstreamProviderRepository providers = Substitute.For<IUpstreamProviderRepository>();
    private readonly InteractiveSignIn signIn;

    public InteractiveSignInTests()
    {
        this.signIn = new InteractiveSignIn(this.users, this.sessions, this.settings, this.providers);
    }

    [Fact]
    public async Task ReadsDelegateToTheExistingRepositories()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
        User user = User.CreateLocal("sign-in@example.test", "Sign In", "fixture-hash", clock).Value;
        UserSession session = UserSession.Create(user.Id, "127.0.0.1", "tests", clock).Value;
        var authenticationSettings = AuthenticationSettings.CreateDefault(clock);
        UpstreamProvider provider = UpstreamProvider.Create("provider", "Provider", "https://issuer.example", "client-id").Value;
        this.users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        this.sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        this.settings.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(authenticationSettings);
        this.providers.GetByIdAsync(provider.Id, Arg.Any<CancellationToken>()).Returns(provider);
        this.providers.GetByNameAsync(provider.Name, Arg.Any<CancellationToken>()).Returns(provider);
        this.providers.GetActiveProvidersAsync(Arg.Any<CancellationToken>()).Returns(new[] { provider });

        (await this.signIn.GetUserAsync(user.Id)).ShouldBeSameAs(user);
        (await this.signIn.GetSessionAsync(session.Id)).ShouldBeSameAs(session);
        (await this.signIn.GetOrCreateAuthenticationSettingsAsync()).ShouldBeSameAs(authenticationSettings);
        (await this.signIn.GetUpstreamProviderAsync(provider.Id)).ShouldBeSameAs(provider);
        (await this.signIn.GetUpstreamProviderByNameAsync(provider.Name)).ShouldBeSameAs(provider);
        (await this.signIn.GetActiveUpstreamProvidersAsync()).ShouldContain(provider);

        await this.users.Received(1).GetByIdAsync(user.Id, Arg.Any<CancellationToken>());
        await this.sessions.Received(1).GetByIdAsync(session.Id, Arg.Any<CancellationToken>());
        await this.settings.Received(1).GetOrCreateAsync(Arg.Any<CancellationToken>());
        await this.providers.Received(1).GetByIdAsync(provider.Id, Arg.Any<CancellationToken>());
        await this.providers.Received(1).GetByNameAsync(provider.Name, Arg.Any<CancellationToken>());
        await this.providers.Received(1).GetActiveProvidersAsync(Arg.Any<CancellationToken>());
    }
}
