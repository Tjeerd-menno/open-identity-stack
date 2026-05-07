using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;
public sealed class ListGroupsQueryHandlerTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ListGroupsQueryHandler _handler;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public ListGroupsQueryHandlerTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
        this._handler = new ListGroupsQueryHandler(this._groupRepository);
    }

    [Fact]
    public async Task HandleAsync_ReturnsGroups_WhenFound()
    {
        // Arrange
        Group group1 = Group.Create("Group One", "First", this._dateTimeProvider).Value;
        Group group2 = Group.Create("Group Two", "Second", this._dateTimeProvider).Value;

        this._groupRepository.ListAsync(1, 20, null, Arg.Any<CancellationToken>())
            .Returns((new List<Group> { group1, group2 }, 2));

        // Act
        Result<GroupListResponse> result = await this._handler.HandleAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count().ShouldBe(2);
        result.Value.Items.First().Name.ShouldBe(group1.Name);
    }

    [Fact]
    public async Task HandleAsync_ReturnsEmpty_WhenNoGroups()
    {
        // Arrange
        this._groupRepository.ListAsync(1, 20, null, Arg.Any<CancellationToken>())
            .Returns((new List<Group>(), 0));

        // Act
        Result<GroupListResponse> result = await this._handler.HandleAsync();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
    }
}
