using Microsoft.Extensions.DependencyInjection;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class SessionsAdminListSessionsTests
{
    [Fact]
    public async Task ListAsync_ResolvesTheSessionsWorkflowAndListsSessions()
    {
        ISessionRepository sessions = Substitute.For<ISessionRepository>();
        sessions.ListAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<UserId?>(),
                Arg.Any<SessionStatus?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns((Array.Empty<UserSession>(), 0));

        ServiceCollection services = new();
        services.AddSingleton(sessions);
        services.AddSingleton(Substitute.For<IDateTimeProvider>());
        services.AddSingleton(Substitute.For<ICredentialTerminationService>());
        services.AddApplication();

        await using ServiceProvider provider = services.BuildServiceProvider();
        ISessionsAdmin admin = provider.GetRequiredService<ISessionsAdmin>();

        Result<ListSessionsResult> result = await admin.ListAsync(new ListSessionsQuery(Page: 1, PageSize: 20));

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(0);
        result.Value.Items.ShouldBeEmpty();
    }
}
