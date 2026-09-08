using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;

public sealed class RequestScopedUserGroupsProviderTests
{
    [Fact]
    public async Task LoadsGroupsOncePerUserWithinScope()
    {
        IGroupRepository repository = Substitute.For<IGroupRepository>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var userId = new UserId(Guid.NewGuid());
        var otherUserId = new UserId(Guid.NewGuid());
        Group group = Group.Create("engineering", null, clock).Value;
        repository.GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>()).Returns([group]);
        repository.GetGroupsForUserAsync(otherUserId, Arg.Any<CancellationToken>()).Returns([]);

        var provider = new RequestScopedUserGroupsProvider(repository);

        IReadOnlyList<Group> first = await provider.GetGroupsForUserAsync(userId);
        IReadOnlyList<Group> second = await provider.GetGroupsForUserAsync(userId);
        IReadOnlyList<Group> other = await provider.GetGroupsForUserAsync(otherUserId);

        first.ShouldBe([group]);
        second.ShouldBeSameAs(first);
        other.ShouldBeEmpty();
        await repository.Received(1).GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>());
        await repository.Received(1).GetGroupsForUserAsync(otherUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetriesAfterFailedLoad()
    {
        IGroupRepository repository = Substitute.For<IGroupRepository>();
        var userId = new UserId(Guid.NewGuid());
        repository.GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<IReadOnlyList<Group>>(new InvalidOperationException("transient")),
                _ => Task.FromResult<IReadOnlyList<Group>>([]));

        var provider = new RequestScopedUserGroupsProvider(repository);

        await Should.ThrowAsync<InvalidOperationException>(() => provider.GetGroupsForUserAsync(userId));
        IReadOnlyList<Group> groups = await provider.GetGroupsForUserAsync(userId);

        groups.ShouldBeEmpty();
        await repository.Received(2).GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>());
    }
}
