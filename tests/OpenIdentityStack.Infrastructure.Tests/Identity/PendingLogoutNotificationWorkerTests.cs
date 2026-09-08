using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Infrastructure.Identity;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class PendingLogoutNotificationWorkerTests
{
    [Fact]
    public async Task ProcessPendingAsync_SuccessfulRetry_PersistsCompletedDelivery()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        UserSession session = UserSession.Create(UserId.Create(), "127.0.0.1", "test", clock).Value;
        session.AddClientSession("portal", clock);
        session.ClientSessions[0].SetLogoutUris(null, "https://portal.example.test/backchannel");
        session.Logout(clock);

        ISessionRepository repository = Substitute.For<ISessionRepository>();
        repository.GetTerminalSessionsWithPendingLogoutNotificationsAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([session]);
        ILogoutNotifier notifier = Substitute.For<ILogoutNotifier>();
        notifier.NotifyClientsAsync(Arg.Any<SessionId>(), Arg.Any<IReadOnlyList<ClientSessionInfo>>(), Arg.Any<CancellationToken>())
            .Returns(new LogoutNotificationResult(1, 0, []));
        PendingLogoutNotificationWorker worker = CreateWorker(repository, notifier, clock);

        await worker.ProcessPendingAsync();

        session.ClientSessions[0].LogoutStatus.ShouldBe(LogoutStatus.Completed);
        await repository.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingAsync_FailedRetry_IsNotRetriedBeforeItsDueTime()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        UserSession session = UserSession.Create(UserId.Create(), "127.0.0.1", "test", clock).Value;
        session.AddClientSession("portal", clock);
        session.ClientSessions[0].SetLogoutUris(null, "https://portal.example.test/backchannel");
        session.Logout(clock);

        ISessionRepository repository = Substitute.For<ISessionRepository>();
        repository.GetTerminalSessionsWithPendingLogoutNotificationsAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([session]);
        ILogoutNotifier notifier = Substitute.For<ILogoutNotifier>();
        notifier.NotifyClientsAsync(Arg.Any<SessionId>(), Arg.Any<IReadOnlyList<ClientSessionInfo>>(), Arg.Any<CancellationToken>())
            .Returns(new LogoutNotificationResult(0, 1, ["portal"]));
        PendingLogoutNotificationWorker worker = CreateWorker(repository, notifier, clock);

        await worker.ProcessPendingAsync();
        await worker.ProcessPendingAsync();

        session.ClientSessions[0].LogoutStatus.ShouldBe(LogoutStatus.Failed);
        await notifier.Received(1).NotifyClientsAsync(
            session.Id,
            Arg.Any<IReadOnlyList<ClientSessionInfo>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingAsync_CancellationLeavesDeliveryPendingForNextWorker()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        UserSession session = UserSession.Create(UserId.Create(), "127.0.0.1", "test", clock).Value;
        session.AddClientSession("portal", clock);
        session.ClientSessions[0].SetLogoutUris(null, "https://portal.example.test/backchannel");
        session.Logout(clock);
        ISessionRepository repository = Substitute.For<ISessionRepository>();
        repository.GetTerminalSessionsWithPendingLogoutNotificationsAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([session]);
        ILogoutNotifier notifier = Substitute.For<ILogoutNotifier>();
        notifier.NotifyClientsAsync(Arg.Any<SessionId>(), Arg.Any<IReadOnlyList<ClientSessionInfo>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LogoutNotificationResult>(new OperationCanceledException()));
        PendingLogoutNotificationWorker worker = CreateWorker(repository, notifier, clock);

        await Should.ThrowAsync<OperationCanceledException>(() => worker.ProcessPendingAsync());

        session.ClientSessions[0].LogoutStatus.ShouldBe(LogoutStatus.Pending);
        await repository.DidNotReceive().UpdateAsync(session, Arg.Any<CancellationToken>());

        notifier.NotifyClientsAsync(Arg.Any<SessionId>(), Arg.Any<IReadOnlyList<ClientSessionInfo>>(), Arg.Any<CancellationToken>())
            .Returns(new LogoutNotificationResult(1, 0, []));
        PendingLogoutNotificationWorker restartedWorker = CreateWorker(repository, notifier, clock);
        await restartedWorker.ProcessPendingAsync();

        session.ClientSessions[0].LogoutStatus.ShouldBe(LogoutStatus.Completed);
        await repository.Received(1).UpdateAsync(session, Arg.Any<CancellationToken>());
    }

    private static PendingLogoutNotificationWorker CreateWorker(
        ISessionRepository repository,
        ILogoutNotifier notifier,
        IDateTimeProvider clock)
    {
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(ISessionRepository)).Returns(repository);
        provider.GetService(typeof(ILogoutNotifier)).Returns(notifier);
        provider.GetService(typeof(IDateTimeProvider)).Returns(clock);
        IServiceScope scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);
        IServiceScopeFactory factory = Substitute.For<IServiceScopeFactory>();
        factory.CreateScope().Returns(scope);
        ILogger<PendingLogoutNotificationWorker> logger = Substitute.For<ILogger<PendingLogoutNotificationWorker>>();
        return new PendingLogoutNotificationWorker(factory, logger);
    }
}
